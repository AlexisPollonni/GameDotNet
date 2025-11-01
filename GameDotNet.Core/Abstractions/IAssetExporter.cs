namespace GameDotNet.Core.Abstractions;

public interface IAssetExporter<in TAsset> where TAsset : IAsset
{
    ValueTask<bool> TryExportAsync(TAsset asset, AssetSource destination, CancellationToken token = default);
}