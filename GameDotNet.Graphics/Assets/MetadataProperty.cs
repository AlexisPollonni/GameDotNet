using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using dotVariant;

namespace GameDotNet.Graphics.Assets;

[Variant]
[SuppressMessage("ReSharper", "PartialMethodWithSinglePart")]
public partial class MetadataProperty
{
    static partial void VariantOf(bool a, int b, ulong c, float d, double e, string f, Vector3 g,
                                  Dictionary<string, MetadataProperty> h);
}