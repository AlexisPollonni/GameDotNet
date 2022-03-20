using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
            var maskGenerator = new ComponentMaskGenerator(context, diagnosticReporter);

            var components = FindComponents(context.Compilation, componentInterface, diagnosticReporter)
                .ToArray();

            storeGenerator.Execute(storeBase, components);
            maskGenerator.Execute();
        }

        private static IEnumerable<Component> FindComponents(Compilation comp, INamedTypeSymbol compInterface,
                                                             DiagnosticReporter diag)
        {
            // Gets the classes that implement IComponent
            var implementedComponents
                = Helpers.FindImplementations(compInterface, comp).ToImmutableList();
            diag.ReportInfo($"Found {implementedComponents.Count} types implementing IComponent",
                            $"Found types : {implementedComponents.Select(symbol => symbol.ToString()).Aggregate((s1, s2) => s1 + ", " + s2)}");


            var components = new List<Component>();
            foreach (var component in implementedComponents.Where(componentSymbol => !componentSymbol.IsGenericType)
                                                           .Select(componentSymbol => new Component(componentSymbol)))
            {
                component.VariableName = GetVariableName(component, components);
                components.Add(component);
            }

            return components;
        }

        private static string GetVariableName(Component component, IReadOnlyCollection<Component> components)
        {
            var typeName = component.ComponentType.ToString();

            var parts = typeName.Split('.');
            for (var i = parts.Length - 1; i >= 0; i--)
            {
                var candidate = string.Join("", parts.Skip(i));
                candidate = "_" + char.ToLowerInvariant(candidate[0]) + candidate.Substring(1);
                if (components.Any(f => string.Equals(f.VariableName, candidate, StringComparison.Ordinal))) continue;
                typeName = candidate;
                break;
            }

            return typeName;
        }
    }
}