namespace GameDotNet.Core.Abstractions;

public interface ISavedSceneAsset : IAsset
{
    IEnumerable<IAsset> SceneAssets { get; }
}