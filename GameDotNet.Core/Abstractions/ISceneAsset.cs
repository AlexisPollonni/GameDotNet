namespace GameDotNet.Core.Abstractions;

public interface ISceneAsset : IAsset
{
    IEnumerable<IAsset> SceneAssets { get; }
}