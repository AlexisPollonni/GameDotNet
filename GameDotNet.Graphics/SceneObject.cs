using System.Numerics;

namespace GameDotNet.Graphics;

public class SceneObject
{
    public string Name { get; }
    public Matrix4x4 Transform { get; }
    public IReadOnlyList<SceneObject> Children => _children;
    public IReadOnlyList<Mesh> Meshes { get; }


    private readonly List<SceneObject> _children;

    public SceneObject(string name, Matrix4x4 transform, IReadOnlyList<Mesh> meshes)
    {
        Name = name;
        Transform = transform;
        Meshes = meshes;
        _children = new();
    }

    public void AddChild(SceneObject child) => _children.Add(child);
}