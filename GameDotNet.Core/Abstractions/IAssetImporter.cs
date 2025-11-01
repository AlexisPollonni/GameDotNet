using System.Diagnostics.CodeAnalysis;

namespace GameDotNet.Core.Abstractions;

public interface IAssetImporter<TAsset> where TAsset : IAsset
{
    ValueTask<bool> TryImportAsync(AssetSource source, [NotNullWhen(true)] out TAsset? asset, CancellationToken token = default);
}