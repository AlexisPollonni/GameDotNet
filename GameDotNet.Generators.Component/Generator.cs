using Microsoft.CodeAnalysis;

namespace GameDotNet.Generators.Component
{
    [Generator]
    public class Generator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        { }

        public void Execute(GeneratorExecutionContext context)
        {
            var diagnosticReporter = new DiagnosticReporter(context);

            var componentInterface = context.Compilation.GetTypeByMetadataName("GameDotNet.Core.ECS.IComponent");
            if (componentInterface is null)
            {
                diagnosticReporter.ReportError("Type GameDotNet.Core.ECS.IComponent not found, this shouldn't happen",
                                               "Please make sure the Core package is included");
                return;
            }

            var storeBase = context.Compilation.GetTypeByMetadataName("GameDotNet.Core.ECS.ComponentStoreBase");
            if (storeBase is null)
            {
                diagnosticReporter
                    .ReportError("Type GameDotNet.ECS.ComponentStoreBase not found, this shouldn't happen",
                                 "Please make sure the Core package is included");
                return;
            }

            var storeGenerator = new ComponentStoreGenerator(context, diagnosticReporter);

            storeGenerator.Execute(componentInterface, storeBase);
        }
    }
}