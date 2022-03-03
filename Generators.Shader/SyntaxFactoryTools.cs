using System;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Generator.Shader;

public static class SyntaxFactoryTools
{
    public static FileScopedNamespaceDeclarationSyntax UsingFileScoped(params string[] usingChunks)
    {
        var identifiers = usingChunks.Select(IdentifierName).ToArray();

        switch (identifiers.Length)
        {
            case 0:
                throw new ArgumentException("Usings identifiers can't be empty", nameof(usingChunks));
            case 1:
                return FileScopedNamespaceDeclaration(identifiers.Single());
            default:
            {
                var name = QualifiedName(identifiers[0], identifiers[1]);
                for (var i = 2; i < identifiers.Length; i++)
                {
                    name = QualifiedName(name, identifiers[i]);
                }

                return FileScopedNamespaceDeclaration(name);
            }
        }
    }

    public static FieldDeclarationSyntax IntFieldWithInit(string name, int init) =>
        FieldDeclaration(VariableDeclaration(PredefinedType(Token(SyntaxKind.IntKeyword)))
                             .AddVariables(VariableDeclarator(Identifier(name))
                                               .WithInitializer(EqualsValueClause(LiteralExpression(SyntaxKind.NumericLiteralExpression,
                                                                    Literal(init))))));
}