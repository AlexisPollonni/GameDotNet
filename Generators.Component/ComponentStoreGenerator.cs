using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Generator.Component
{
    public class ComponentStoreGenerator
    {
        private readonly GeneratorExecutionContext _context;
        private readonly IDiagnosticReporter _diagnosticReporter;

        public ComponentStoreGenerator(in GeneratorExecutionContext context, IDiagnosticReporter diagnosticReporter)
        {
            _context = context;
            _diagnosticReporter = diagnosticReporter;
        }

        public void Execute(INamedTypeSymbol componentInterface, INamedTypeSymbol componentStoreInterface)
        {
            // Gets the classes that implement IComponent
            var implementedComponents
                = Helpers.FindImplementations(componentInterface, _context.Compilation).ToImmutableList();
            _diagnosticReporter.ReportInfo($"Found {implementedComponents.Count} types implementing IComponent",
                                           $"Found types : {implementedComponents.Select(symbol => symbol.ToString()).Aggregate((s1, s2) => s1 + ", " + s2)}");


            var components = new List<Component>();
            foreach (var componentSymbol in implementedComponents)
            {
                if (componentSymbol.IsGenericType) break;

                var component = new Component(componentSymbol);
                components.Add(component);
                component.VariableName = GetVariableName(component, components);
            }

            GenerateComponentStore(components, componentStoreInterface);
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

        private void GenerateComponentStore(IReadOnlyCollection<Component> components,
                                            INamedTypeSymbol componentStoreInterface)
        {
            var generatedSource = $@"
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Core.ECS;

namespace Core.ECS.Generated
{{
    public class ComponentStore : {componentStoreInterface}
    {{
{GenerateFields(components).AddTabulations(2).ConcatLines()}

        public ulong Add<T>() where T : struct, IComponent
        {{
            ref var l = ref GetList<T>();

            var c = new T();
            l.Add(in c);

            return l.Count - 1;
        }}

        public ulong Add<T>(in T component) where T : struct, IComponent
        {{
            ref var l = ref GetList<T>();

            l.Add(component);
            return l.Count - 1;
        }}

        public ref T Get<T>(ulong index) where T : struct, IComponent =>
            ref GetList<T>()[index];

{GenerateListAccessor(components).AddTabulations(2).ConcatLines()}
    }}
}}";

            _context.AddSource("ComponentStore.Generated.cs", SourceText.From(generatedSource, Encoding.UTF8));
        }

        private static IEnumerable<string> GenerateFields(IEnumerable<Component> components) =>
            components.Select(
                              component =>
                                  $"private RefStructList<{component.ComponentType}> {component.VariableName} = new ();");

        private static IEnumerable<string> GenerateListAccessor(IReadOnlyCollection<Component> components)
        {
            var body = components.Select(c => new[]
                                 {
                                     $"if (typeof(T) == typeof({c.ComponentType}))",
                                     $"return ref Unsafe.As<RefStructList<{c.ComponentType}>, RefStructList<T>>(ref {c.VariableName});"
                                         .AddTabulation()
                                 })
                                 .SelectMany(e => e)
                                 .Append(Environment.NewLine)
                                 .Append(
                                         "throw new InvalidOperationException($\"Type {typeof(T).FullName} was not found in the component store, this shouldn't happen.\");");

            return Enumerable.Empty<string>()
                             .Append("private ref RefStructList<T> GetList<T>() where T : struct, IComponent")
                             .Append("{")
                             .Concat(body.AddTabulations())
                             .Append("}");
        }
    }
}