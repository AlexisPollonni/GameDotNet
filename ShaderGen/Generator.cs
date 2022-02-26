using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Vortice.ShaderCompiler;

namespace Generator.Shader
{
    [Generator]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    public class Generator : ISourceGenerator
    {
        private static readonly DiagnosticDescriptor FileInfo = new("SHAGEN001",
                                                                    "Found additional files",
                                                                    "Found {0} additional files",
                                                                    "ShaderGenerator",
                                                                    DiagnosticSeverity.Warning,
                                                                    true);

        private readonly Compiler _shaderCompiler;

        public Generator()
        {
            using var options = new Options();
            options.SetSourceLanguage(SourceLanguage.GLSL);
            options.SetargetSpirv(SpirVVersion.Version_1_5);

            _shaderCompiler = new(options);
        }

        public void Initialize(GeneratorInitializationContext context)
        { }

        public void Execute(GeneratorExecutionContext context)
        {
            Debugger.Launch();
            context.ReportDiagnostic(Diagnostic.Create(FileInfo, Location.None, context.AdditionalFiles.Length));

            var files = context.AdditionalFiles.Where(text =>
                               {
                                   var ext = Path.GetExtension(text.Path);

                                   return ext is "vert" or "frag" or "glsl";
                               })
                               .ToArray();

            Console.WriteLine($"Found {files.Length} shader files to compile : {string.Join(Environment.NewLine, files.Select(text => text.Path))}");

            var spirV = new List<string>();
            foreach (var shaderFile in files)
            {
                var content = shaderFile.GetText();
                var kind = Path.GetExtension(shaderFile.Path) switch
                {
                    "vert" => ShaderKind.VertexShader,
                    "frag" => ShaderKind.FragmentShader,
                    _ => throw new ArgumentOutOfRangeException()
                };
                if (content is not null)
                {
                    var str = content.ToString();

                    var res = _shaderCompiler.Compile(str, Path.GetFileNameWithoutExtension(shaderFile.Path), kind);

                    if (res.Status == CompilationStatus.Success)
                    {
                        var bc = res.GetBytecode();
                        var bytecodeStr = Encoding.UTF8.GetString(bc.ToArray());
                        spirV.Add(bytecodeStr);
                    }
                }
            }
        }
    }
}