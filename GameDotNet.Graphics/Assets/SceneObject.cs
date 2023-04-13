using GameDotNet.Core.Physics.Components;

namespace GameDotNet.Graphics.Assets;

public class SceneObject
{
    public string Name { get; }

    public Transform Transform { get; }

    public IReadOnlyList<SceneObject> Children => _children;
    public IReadOnlyList<Mesh> Meshes { get; }
    public IReadOnlyDictionary<string, MetadataProperty>? Metadata { get; }


    private readonly List<SceneObject> _children;

    public SceneObject(string name, in Transform transform, IReadOnlyList<Mesh> meshes,
                       IReadOnlyDictionary<string, MetadataProperty>? metadata)
    {
        Name = name;
        Transform = transform;
        Meshes = meshes;
        Metadata = metadata;
        _children = new();
    }

    public SceneObject(string name = "")
        : this(name, new(), Array.Empty<Mesh>(), null)
    { }

    public void AddChild(SceneObject child) => _children.Add(child);
}