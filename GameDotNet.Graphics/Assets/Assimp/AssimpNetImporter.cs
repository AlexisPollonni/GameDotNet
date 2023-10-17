using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using Assimp;
using CommunityToolkit.HighPerformance;
using GameDotNet.Core.Tools.Extensions;
using Serilog;
using Matrix4x4 = Assimp.Matrix4x4;
using Quaternion = Assimp.Quaternion;

namespace GameDotNet.Graphics.Assets.Assimp;

public sealed class AssimpNetImporter : IDisposable
{
    private readonly AssimpContext _lib;
    private readonly SerilogLogStream _logStream;

    public AssimpNetImporter()
    {
        _lib = new();
        _logStream = new();
    }

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
        var vertPositions = mesh.Vertices.AsSpan().Cast<Vector3D, Vector3>();
        var normals = mesh.Normals.AsSpan().Cast<Vector3D, Vector3>();

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

        if (colors.IsEmpty)
        {
            var defaultColors = new Vector4[mesh.VertexCount];
            Array.Fill(defaultColors, Color.White.ToVector4());
            colors = defaultColors;
        }

        var vertices = new List<Vertex>(mesh.FaceCount * 3);
        foreach (ref readonly var face in mesh.Faces.AsSpan())
        {
            if (face.IndexCount != 3)
            {
                Log.Warning("Mesh {Name} has a face with {NberVertices}, this is not supported. Face was skipped...",
                            mesh.Name, face.IndexCount);
                continue;
            }

            foreach (var index in face.Indices)
            {
                vertices.Add(new(vertPositions[index], normals[index], colors[index]));
            }
        }

        return new(vertices);
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
        transform.Decompose(out var scale, out var rot, out var pos);

        return new(node.Name, new(scale.AsVector3(), rot.ToQuaternion(), pos.AsVector3()),
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
                MetaDataType.Bool => new((bool)entry.Data),
                MetaDataType.Int32 => new((int)entry.Data),
                MetaDataType.UInt64 => new((ulong)entry.Data),
                MetaDataType.Float => new((float)entry.Data),
                MetaDataType.Double => new((double)entry.Data),
                MetaDataType.String => new((string)entry.Data),
                MetaDataType.Vector3D => new(((Vector3D)entry.Data).AsVector3()),
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
    public static ref readonly Vector3 AsVector3(this in Vector3D vec)
    {
        return ref Unsafe.As<Vector3D, Vector3>(ref Unsafe.AsRef(vec));
    }

    public static ref readonly System.Numerics.Matrix4x4 AsMatrix4X4(this scoped in Matrix4x4 mat)
    {
        return ref Unsafe.As<Matrix4x4, System.Numerics.Matrix4x4>(ref Unsafe.AsRef(mat));
    }

    public static System.Numerics.Quaternion ToQuaternion(this in Quaternion quaternion)
    {
        return new(quaternion.X, quaternion.Y, quaternion.Z, quaternion.W);
    }
}