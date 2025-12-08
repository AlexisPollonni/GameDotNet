using System.Reflection;
using dotVariant;
using TruePath;

namespace GameDotNet.Core.Abstractions;

public interface IAssetImporter<TAsset> where TAsset : IAsset
{
    ValueTask<Result<TAsset>> ImportAsync(AssetSource source, CancellationToken token = default);
}

[Variant]
public readonly partial struct AssetSource
{
    static partial void VariantOf(LocalPath filePath,
        EmbeddedResource embedded,
        Uri webUri,
        Stream dataStream);
}

public readonly record struct EmbeddedResource(Assembly SourceAssembly, string ResourceName);