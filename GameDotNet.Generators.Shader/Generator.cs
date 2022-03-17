using System;
using System.Collections.Generic;
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
[SuppressMessage("ReSharper", "StringLiteralTypo")]
public class Generator : ISourceGenerator
{
    private static readonly DiagnosticDescriptor FileInfo = new("SHAGEN001",
                                                                "Found additional files",
                                                                "Found {0} additional files",
                                                                "ShaderGenerator",
                                                                DiagnosticSeverity.Info,
                                                                true),
                                                 FileProcess = new("SHAGEN002",
                                                                   "Found shader file",
                                                                   "Found shader file {0}",
                                                                   "ShaderGenerator",
                                                                   DiagnosticSeverity.Info,
                                                                   true);


    public void Initialize(GeneratorInitializationContext context)
    { }

    public void Execute(GeneratorExecutionContext context)
    {
        using var options = new Options
        {
            SourceLanguage = SourceLanguage.Glsl,
            TargetSpirVVersion = new(1, 5)
        };

        using var compiler = new Compiler(options);

        context.ReportDiagnostic(Diagnostic.Create(FileInfo, Location.None, context.AdditionalFiles.Length));

        var spirV = new List<Shader>();
        foreach (var shaderFile in context.AdditionalFiles)
        {
            context.ReportDiagnostic(Diagnostic.Create(FileProcess, Location.None, shaderFile.Path));

            var name = Path.GetFileNameWithoutExtension(shaderFile.Path);
            ShaderKind? kind = Path.GetExtension(shaderFile.Path) switch
            {
                ".vert" => ShaderKind.GlslVertexShader,
                ".frag" => ShaderKind.GlslFragmentShader,
                _ => null
            };
            if (kind is null)
                continue;

            var content = shaderFile.GetText()?.ToString();
            if (content is null)
                continue;

            var res = compiler.Compile(content, name, kind.Value);

            if (res.Status is not Status.Success) continue;

            unsafe
            {
                var spirvBytes = new ReadOnlySpan<byte>((void*)res.CodePointer, (int)res.CodeLength);

                var bytecodeStr = Convert.ToBase64String(spirvBytes.ToArray());
                spirV.Add(new(name, kind.Value, bytecodeStr));
            }
        }


        var shaderFields = spirV.Select(s => CreateShaderStringField(s.FieldName, s.Code))
                                .ToArray();

        var shaderProperties = spirV.Select(s => CreateShaderByteArrayProperty(s.PropertyName, s.FieldName))
                                    .ToArray();


        var tree = SyntaxTree(CompilationUnit()
                              .AddMembers(UsingFileScoped("GameDotNet", "Core", "Shaders", "Generated"),
                                          ClassDeclaration("CompiledShaders")
                                              .AddModifiers(Token(SyntaxKind.InternalKeyword),
                                                            Token(SyntaxKind.StaticKeyword))
                                              .AddMembers(CreateShaderCountConstField("ShaderCount", spirV.Count))
                                              .AddMembers(shaderProperties)
                                              .AddMembers(shaderFields))
                              .NormalizeWhitespace(),
                              encoding: Encoding.UTF8); //Encoding important or GetText throws exception

        context.AddSource("compiledSpirVShaders.g.cs", tree.GetText());
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

    public Shader(string name, ShaderKind kind, string code)
    {
        Name = name;
        Kind = kind;
        Code = code;
    }
}