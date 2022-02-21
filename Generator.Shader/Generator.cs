using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Vortice.ShaderCompiler;

namespace Generator.Shader
{
    [Generator]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    public class Generator : ISourceGenerator
    {
        private static readonly DiagnosticDescriptor FileInfo = new DiagnosticDescriptor(
         "SHAGEN001",
         "Found additional files",
         "Found {0} additional files",
         "ShaderGenerator",
         DiagnosticSeverity.Warning,
         true);

        private Compiler _shaderCompiler;

        public Generator()
        {
            var options = new Options();
            options.SetSourceLanguage(SourceLanguage.GLSL);

            _shaderCompiler = new Compiler(options);
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

                                   return ext == "vert" || ext == "frag" || ext == "glsl";
                               })
                               .ToArray();

            Console.WriteLine(
                              $"Found {files.Length} shader files to compile : {string.Join(Environment.NewLine, files.Select(text => text.Path))}");
        }
    }
}