using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using shaderc;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static GameDotNet.Generators.Shared.SyntaxFactoryTools;

namespace GameDotNet.Generators.Shader;

[Generator]
public class Generator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor FileInfo = new("SHAGEN001",
                                                                "Found additional files",
                                                                "Found {0} additional files",
                                                                "ShaderGenerator",
                                                                DiagnosticSeverity.Info,
                                                                true),
                                                 FileCompiled = new("SHAGEN002",
                                                                   "Shader file compile success",
                                                                   "COMPILED: {0} shader {1}",
                                                                   "ShaderGenerator",
                                                                   DiagnosticSeverity.Info,
                                                                   true),
                                                FileFailed = new("SHAGEN003",
                                                                 "Shader file compile fail",
                                                                 " {0} shader {1} compile error: {2}, {3}",
                                                                 "ShaderGenerator",
                                                                 DiagnosticSeverity.Warning,
                                                                 true);


    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var shaderFiles = context.AdditionalTextsProvider
                                 .Select(static (text, _) => (text, Kind: PathToShaderKind(text.Path)))
                                 .Where(static tuple => tuple.Kind is not null);

        context.RegisterSourceOutput(shaderFiles.Collect(),
                                     (ctx, texts) =>
                                     {
                                         ctx.ReportDiagnostic(Diagnostic.Create(FileInfo, Location.None, texts.Length));
                                     });

        var compilation = shaderFiles.Select(static (tuple, token) =>
                                                 (tuple.text.Path, 
                                                  tuple.Kind,
                                                  Content: tuple.text.GetText(token)?.ToString()))
                                     .Where(tuple => tuple.Content is not null)
                                     .Select((tuple, token) =>
                                     {
                                         using var options = new Options();
                                         options.TargetSpirVVersion = new(1, 5);
                                         options.SourceLanguage = SourceLanguage.Glsl;

                                         using var compiler = new Compiler(options);

                                         token.ThrowIfCancellationRequested();

                                         var res = compiler.Compile(tuple.Path, tuple.Kind!.Value);

                                         token.ThrowIfCancellationRequested();

                                         string byteCode64;
                                         unsafe
                                         {
                                             var spirvBytes =
                                                 new ReadOnlySpan<byte>((void*)res.CodePointer, (int)res.CodeLength);

                                             byteCode64 = Convert.ToBase64String(spirvBytes.ToArray());
                                         }

                                         return (tuple.Path, Kind: tuple.Kind.Value, res.Status, res.ErrorMessage, 
                                                 ByteCode64: byteCode64);
                                     });

        context.RegisterSourceOutput(compilation, (ctx, tuple) =>
        {
            ctx.ReportDiagnostic(tuple.Status is Status.Success
                                     ? Diagnostic.Create(FileCompiled, Location.None, tuple.Kind, tuple.Path)
                                     : Diagnostic.Create(FileFailed, Location.None, tuple.Kind, tuple.Path,
                                                         tuple.Status, tuple.ErrorMessage));
        });
        
        var sourceGen = compilation
            .Where(tuple => tuple.Status == Status.Success)
            .Collect()
            .Select((compRes, token) =>
            {
                var shaders = compRes.Select(tuple => new Shader(tuple.Path, tuple.Kind, tuple.ByteCode64)).ToArray();

                var shaderFields = shaders.Select(s => CreateShaderStringField(s.FieldName, s.Code))
                                          .ToArray();

                var shaderProperties = shaders.Select(s => CreateShaderByteArrayProperty(s.PropertyName, s.FieldName))
                                              .ToArray();


                var tree = SyntaxTree(CompilationUnit()
                                      .AddMembers(UsingFileScoped("GameDotNet", "Core", "Shaders", "Generated"),
                                                  ClassDeclaration("CompiledShaders")
                                                      .AddModifiers(Token(SyntaxKind.InternalKeyword),
                                                                    Token(SyntaxKind.StaticKeyword))
                                                      .AddMembers(CreateShaderCountConstField("ShaderCount",
                                                                      compRes.Length))
                                                      .AddMembers(shaderProperties)
                                                      .AddMembers(shaderFields))
                                      .NormalizeWhitespace(),
                                      encoding: Encoding.UTF8); //Encoding important or GetText throws exception

                token.ThrowIfCancellationRequested();

                return tree.GetText(token);
            });
        
        context.RegisterSourceOutput(sourceGen, (ctx, source) => ctx.AddSource("compiledSpirVShaders.g.cs", source));
    }

    
    
    private static ShaderKind? PathToShaderKind(string path)
    {
        ShaderKind? kind = null;
        const StringComparison comp = StringComparison.OrdinalIgnoreCase;

        if (path.EndsWith(".vert", comp))
            kind = ShaderKind.GlslVertexShader;
        else if (path.EndsWith(".frag", comp))
            kind = ShaderKind.GlslFragmentShader;

        return kind;
    }
    
    private static MemberDeclarationSyntax CreateShaderCountConstField(string name, int count) =>
        ParseMemberDeclaration($"public const int {name} = {count};");

    private static MemberDeclarationSyntax CreateShaderStringField(string fieldName, string code)
        => ParseMemberDeclaration($"private const string {fieldName} = \"{code}\";");

    private static MemberDeclarationSyntax CreateShaderByteArrayProperty(string propertyName, string fieldName) =>
        ParseMemberDeclaration($"public static byte[] {propertyName} {{ get; }} = Convert.FromBase64String({fieldName});");
}

internal sealed class Shader
{
    public string Name { get; }
    public ShaderKind Kind { get; }
    public string Code { get; }

    public string FieldName => $"ShaderStr_{Name}_{Kind}";
    public string PropertyName => $"{Name}{Kind}";

    public Shader(string path, ShaderKind kind, string code)
    {
        Name = Path.GetFileNameWithoutExtension(path);
        Kind = kind;
        Code = code;
    }
}