namespace GameDotNet.Graphics;

public class Scene
{
    public Scene(Mesh[] meshes, SceneObject root)
    {
        Meshes = meshes;
        Root = root;
    }

    public Mesh[] Meshes { get; }
    public SceneObject Root { get; }
}