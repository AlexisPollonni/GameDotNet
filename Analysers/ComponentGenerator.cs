using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Analysers
{
    [Generator]
    public class ComponentGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context) { }

        public void Execute(GeneratorExecutionContext context)
        {
            var diagnosticReporter = new DiagnosticReporter(context);

            var componentInterface = context.Compilation.GetTypeByMetadataName("Core.ECS.IComponent");
            if (componentInterface is null)
            {
                diagnosticReporter.ReportError("Type Core.ECS.IComponent not found, this shouldn't happen", "");
                return;
            }

            // Gets the classes that implement IComponent
            var implementedComponents = FindImplementations(componentInterface, context.Compilation).ToImmutableList();
            diagnosticReporter.ReportInfo($"Found {implementedComponents.Count} types implementing IComponent",
                                          $"Found types : {implementedComponents.Select(symbol => symbol.ToString()).Aggregate((s1, s2) => s1 + ", " + s2)}");


            var components = new List<Component>();

            foreach (var componentSymbol in implementedComponents)
            {
                if (componentSymbol.IsGenericType) break;

                var component = new Component(componentSymbol);
                components.Add(component);
                component.VariableName = GetVariableName(component, components);
            }

            GenerateComponentStore(context, components);
        }

        private static void GenerateComponentStore(GeneratorExecutionContext context, List<Component> components)
        {
            var generatedSource = components.Count == 0
                ? @"
using System;
using Core.ECS;

namespace Core.ECS.Generated
{

    public partial class ComponentStore
    {
        public T Get<T>(ulong id) where T : struct, IComponent
        {
            throw new InvalidOperationException(""Unreachable code called"");
        }
    }
}"
                : $@"
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Core.ECS;

namespace Core.ECS.Generated
{{
    public partial class ComponentStore
    {{
        {GenerateFields(components)}

        {GenerateAccessors(components)}
    }}
}}";

            context.AddSource("ComponentStore.Generated.cs", SourceText.From(generatedSource, Encoding.UTF8));
        }

        private static string GenerateFields(List<Component> components)
        {
            var builder = new StringBuilder();
            foreach (var c in components)
                builder.AppendLine($"private RefStructList<{c.ComponentType}> {c.VariableName} = new ();");

            return builder.ToString();
        }

        private static string GenerateAccessors(List<Component> components)
        {
            var builder = new StringBuilder();

            builder.AppendLine(@"
public ref T Get<T>(ulong id) where T : struct, IComponent
{");

            foreach (var c in components)
            {
                if (c != components.Last())
                    builder.AppendLine($@"
if(typeof(T) == typeof({c.ComponentType}))");

                builder.AppendLine($"   return ref Unsafe.As<{c.ComponentType}, T>(ref {c.VariableName}[id]);");
            }

            if (components.Count == 0)
                builder.AppendLine("throw new System.InvalidOperationException(\"This code is unreachable.\");");
            builder.AppendLine("}");

            return builder.ToString();
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

        private static IEnumerable<INamedTypeSymbol> FindImplementations(ITypeSymbol typeToFind, Compilation compilation)
        {
            foreach (var x in GetAllTypes(compilation.GlobalNamespace.GetNamespaceMembers()))
                if (!x.IsAbstract && x.Interfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, typeToFind)))
                    yield return x;
        }

        private static IEnumerable<INamedTypeSymbol> GetAllTypes(IEnumerable<INamespaceSymbol> namespaces)
        {
            foreach (var ns in namespaces)
            {
                foreach (var t in ns.GetTypeMembers()) yield return t;

                foreach (var subType in GetAllTypes(ns.GetNamespaceMembers())) yield return subType;
            }
        }
    }

    internal class Component
    {
        public INamedTypeSymbol ComponentType { get; }
        public string VariableName { get; internal set; }

        public Component(INamedTypeSymbol componentType)
        {
            ComponentType = componentType;
        }
    }
}