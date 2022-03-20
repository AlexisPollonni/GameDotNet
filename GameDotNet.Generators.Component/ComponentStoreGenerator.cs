using System.Collections.Generic;
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

        public void Execute(INamedTypeSymbol componentStoreBase, IReadOnlyCollection<Component> components)
        {
            GenerateComponentStore(components, componentStoreBase);
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
                                  ParseMemberDeclaration($"private ComponentPool<{component.ComponentType}> {component.VariableName} = new ();"));

        private static MethodDeclarationSyntax GenerateListAccessor(IEnumerable<Component> components)
        {
            var ifConditions = components.Select(c =>
                                                     IfStatement(ParseExpression($"typeof(T) == typeof({c.ComponentType})"),
                                                                 ParseStatement($"return ref Unsafe.As<ComponentPool<{c.ComponentType}>, ComponentPool<T>>(ref {c.VariableName});")));

            var throwEnd =
                ParseStatement("throw new InvalidOperationException($\"Type {typeof(T).FullName} was not found in the component store, this shouldn't happen.\");");

            return MethodDeclaration(RefType(GenericName("ComponentPool")
                                                 .AddTypeArgumentListArguments(IdentifierName("T"))), "GetPool")
                   .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.OverrideKeyword))
                   .AddTypeParameterListParameters(TypeParameter("T"))
                   .WithBody(Block(ifConditions)
                                 .AddStatements(throwEnd));
        }
    }
}