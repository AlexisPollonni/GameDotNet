using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace GameDotNet.Generators.Component
{
    public static class Helpers
    {
        public static IEnumerable<INamedTypeSymbol> FindImplementations(ITypeSymbol typeToFind, Compilation compilation)
        {
            foreach (var x in GetAllTypes(compilation.GlobalNamespace.GetNamespaceMembers()))
                if (!x.IsAbstract && x.Interfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, typeToFind)))
                    yield return x;
        }

        public static IEnumerable<INamedTypeSymbol> GetAllTypes(IEnumerable<INamespaceSymbol> namespaces)
        {
            foreach (var ns in namespaces)
            {
                foreach (var t in ns.GetTypeMembers()) yield return t;

                foreach (var subType in GetAllTypes(ns.GetNamespaceMembers())) yield return subType;
            }
        }

        public static string AddTabulation(this string line, int indent = 1) =>
            line.PadLeft(line.Length + indent, '\t');

        public static IEnumerable<string> AddTabulations(this IEnumerable<string> lines, int indent = 1) =>
            lines.Select(s => s.AddTabulation(indent));

        public static string ConcatLines(this IEnumerable<string> lines) =>
            lines.Aggregate((line1, line2) => line1 + Environment.NewLine + line2) + Environment.NewLine;

        public static IEnumerable<T> SkipLast<T>(this IEnumerable<T> source, int n)
        {
            using var it = source.GetEnumerator();
            bool hasRemainingItems;
            var cache = new Queue<T>(n + 1);

            do
            {
                // ReSharper disable once AssignmentInConditionalExpression
                if (hasRemainingItems = it.MoveNext())
                {
                    cache.Enqueue(it.Current);
                    if (cache.Count > n) yield return cache.Dequeue();
                }
            } while (hasRemainingItems);
        }
    }
}