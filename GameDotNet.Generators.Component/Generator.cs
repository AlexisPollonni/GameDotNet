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

            var storeInterface = context.Compilation.GetTypeByMetadataName("GameDotNet.Core.ECS.IComponentStore");
            if (storeInterface is null)
            {
                diagnosticReporter.ReportError("Type GameDotNet.ECS.IComponentStore not found, this shouldn't happen",
                                               "Please make sure the Core package is included");
                return;
            }

            var storeGenerator = new ComponentStoreGenerator(in context, diagnosticReporter);

            storeGenerator.Execute(componentInterface, storeInterface);
        }
    }
}