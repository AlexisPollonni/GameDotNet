using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Numerics;
using Assimp;
using Assimp.Unmanaged;
using CommunityToolkit.HighPerformance;
using Silk.NET.Assimp;
using Metadata = Assimp.Metadata;
using Node = Assimp.Node;
using PostProcessPreset = Assimp.PostProcessPreset;

namespace GameDotNet.Graphics.Assets.Assimp;

public sealed class AssimpNetImporter : IDisposable
{
    private readonly AssimpContext _lib = new();
    private readonly SerilogLogStream _logStream = new();

    public bool LoadSceneFromFile(string path, out Scene? scene)
    {
        var model = _lib.ImportFile(path, PostProcessPreset.TargetRealTimeFast);

        var metadata = GetMetadata(model.Metadata);
        var meshes = model.Meshes.Select(LoadMesh).ToArray();

        var sceneRoot = new SceneObject("Scene");
        CopyNodesWithMeshes(model.RootNode, sceneRoot, null, meshes);

        scene = new(meshes, sceneRoot, metadata);
        return true;
    }

    public void Dispose()
    {
        _lib.Dispose();
        _logStream.Dispose();
    }

    private static Mesh LoadMesh(global::Assimp.Mesh mesh)
    {
        var vertPositions = mesh.Vertices.AsSpan();
        var normals = mesh.Normals.AsSpan();
        
        //Choose first color channel
        var colors = ReadOnlySpan<Vector4>.Empty;
        if (mesh.VertexColorChannelCount is not 0)
        {
            for (var i = 0; i < mesh.VertexColorChannels.Length; i++)
            {
                if (!mesh.HasVertexColors(i)) continue;

                //TODO: Colors might be wrong layout to render, here its RGBA
                var colorSet = mesh.VertexColorChannels[i].AsSpan().Cast<Color4D, Vector4>();

                colors = colorSet;
                break;
            }
        }

        var vertices = new Vertex[vertPositions.Length];
        if (colors.IsEmpty)
            for (var i = 0; i < vertPositions.Length; i++)
                vertices[i] = new(vertPositions[i], normals[i], Color.White);
        else
            for (var i = 0; i < vertPositions.Length; i++)
                vertices[i] = new(vertPositions[i], normals[i], colors[i]);

        return new(vertices, mesh.GetUnsignedIndices());
    }

    private static void CopyNodesWithMeshes(Node node, SceneObject targetParent, in Matrix4x4? accTransform,
                                            IReadOnlyList<Mesh> loadedMeshes)
    {
        SceneObject parent;
        Matrix4x4 transform;

        var nodeTransformAcc = accTransform * node.Transform ?? node.Transform;

        if (node.HasMeshes)
        {
            var newObject = CreateObjectFromNode(node, nodeTransformAcc, loadedMeshes);

            targetParent.AddChild(newObject);

            parent = newObject;
            transform = Matrix4x4.Identity;
        }
        else
        {
            parent = targetParent;
            transform = nodeTransformAcc;
        }

        foreach (var child in node.Children)
        {
            CopyNodesWithMeshes(child, parent, transform, loadedMeshes);
        }
    }

    [SuppressMessage("ReSharper", "PossiblyImpureMethodCallOnReadonlyVariable")]
    private static SceneObject CreateObjectFromNode(Node node, in Matrix4x4 transform, IReadOnlyList<Mesh> loadedMeshes)
    {
        var metadata = GetMetadata(node.Metadata);
        
        // We use assimp's function and not the system ones because assimp matrixes are different
        AssimpLibrary.Instance.DecomposeMatrix(transform, out var scale, out var rot, out var pos);

        return new(node.Name, new(pos, rot, scale),
                   GetMeshesFromNode(node, loadedMeshes), metadata);
    }

    private static IReadOnlyList<Mesh> GetMeshesFromNode(Node node, IReadOnlyList<Mesh> loadedMeshes)
    {
        var meshList = new List<Mesh>(node.MeshCount);

        meshList.AddRange(node.MeshIndices.Select(index => loadedMeshes[index]));

        return meshList;
    }

    private static IReadOnlyDictionary<string, MetadataProperty> GetMetadata(Metadata data)
    {
        var result = new Dictionary<string, MetadataProperty>(data.Count);

        foreach (var (key, entry) in data)
        {
            MetadataProperty prop = entry.DataType switch
            {
                MetadataType.Bool => new((bool)entry.Data),
                MetadataType.Int32 => new((int)entry.Data),
                MetadataType.Uint64 => new((ulong)entry.Data),
                MetadataType.Float => new((float)entry.Data),
                MetadataType.Double => new((double)entry.Data),
                MetadataType.Aistring => new((string)entry.Data),
                MetadataType.Aivector3D => new((Vector3)entry.Data),
                _ => throw new ArgumentOutOfRangeException(nameof(entry.DataType),
                                                           "Metadata entry type is out of range")
            };

            result.Add(key, prop);
        }

        return result;
    }
}

internal static class AssimpEx
{
}