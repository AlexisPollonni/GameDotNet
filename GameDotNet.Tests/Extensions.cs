using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace GameDotNet.Tests;

internal static class Extensions
{
    public static IEnumerable<T> Append<T>(this IEnumerable<T> e, params T[] list) => e.Concat(list);

    public static MetadataReference GetTypeMetadataRef(this Type t) =>
        MetadataReference.CreateFromFile(t.Assembly.Location);
}