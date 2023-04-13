namespace GameDotNet.Graphics.Assets;

public class Scene
{
    public Scene(Mesh[] meshes, SceneObject root, IReadOnlyDictionary<string, MetadataProperty>? metadata)
    {
        Meshes = meshes;
        Root = root;
        Metadata = metadata;
    }

    public IReadOnlyDictionary<string, MetadataProperty>? Metadata { get; }
    public Mesh[] Meshes { get; }
    public SceneObject Root { get; }
}