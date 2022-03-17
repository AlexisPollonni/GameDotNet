using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static GameDotNet.Generators.Shared.SyntaxFactoryTools;

namespace GameDotNet.Generators.Component
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

        public void Execute(INamedTypeSymbol componentInterface, INamedTypeSymbol componentStoreBase)
        {
            // Gets the classes that implement IComponent
            var implementedComponents
                = Helpers.FindImplementations(componentInterface, _context.Compilation).ToImmutableList();
            _diagnosticReporter.ReportInfo($"Found {implementedComponents.Count} types implementing IComponent",
                                           $"Found types : {implementedComponents.Select(symbol => symbol.ToString()).Aggregate((s1, s2) => s1 + ", " + s2)}");


            var components = new List<Component>();
            foreach (var component in implementedComponents.Where(componentSymbol => !componentSymbol.IsGenericType)
                                                           .Select(componentSymbol => new Component(componentSymbol)))
            {
                component.VariableName = GetVariableName(component, components);
                components.Add(component);
            }

            GenerateComponentStore(components, componentStoreBase);
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
                                            INamedTypeSymbol componentStoreBase)
        {
            var cu = CompilationUnit()
                     .WithUsings("System",
                                 "System.Collections.Generic",
                                 "System.Runtime.CompilerServices",
                                 "GameDotNet.Core.ECS",
                                 "GameDotNet.Core.Tools.Containers")
                     .AddMembers(UsingFileScoped("GameDotNet", "Core", "ECS", "Generated"),
                                 ClassDeclaration("ComponentStore")
                                     .AddModifiers(Token(SyntaxKind.InternalKeyword))
                                     .AddMembers(GenerateFields(components).ToArray())
                                     .AddMembers(GenerateListAccessor(components))
                                     .AddBaseListTypes(SimpleBaseType(IdentifierName(componentStoreBase.Name))));

            var tree = SyntaxTree(cu.NormalizeWhitespace(), encoding: Encoding.UTF8);

            _context.AddSource("ComponentStore.g.cs", tree.GetText());
        }

        private static IEnumerable<MemberDeclarationSyntax> GenerateFields(IEnumerable<Component> components) =>
            components.Select(component =>
                                  ParseMemberDeclaration($"private RefStructList<{component.ComponentType}> {component.VariableName} = new ();"));

        private static MethodDeclarationSyntax GenerateListAccessor(IEnumerable<Component> components)
        {
            var ifConditions = components.Select(c =>
                                                     IfStatement(ParseExpression($"typeof(T) == typeof({c.ComponentType})"),
                                                                 ParseStatement($"return ref Unsafe.As<RefStructList<{c.ComponentType}>, RefStructList<T>>(ref {c.VariableName});")));

            var throwEnd =
                ParseStatement("throw new InvalidOperationException($\"Type {typeof(T).FullName} was not found in the component store, this shouldn't happen.\");");

            return MethodDeclaration(RefType(GenericName("RefStructList")
                                                 .AddTypeArgumentListArguments(IdentifierName("T"))), "GetList")
                   .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.OverrideKeyword))
                   .AddTypeParameterListParameters(TypeParameter("T"))
                   .WithBody(Block(ifConditions)
                                 .AddStatements(throwEnd));
        }
    }
}