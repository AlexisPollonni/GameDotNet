using System.Drawing;
using System.Numerics;
using CommunityToolkit.HighPerformance;
using GameDotNet.Core.Tools.Extensions;
using Serilog;
using Silk.NET.Assimp;

namespace GameDotNet.Graphics;

public class AssetImporter : IDisposable
{
    private readonly Assimp _assimp;

    public AssetImporter()
    {
        _assimp = Assimp.GetApi();
    }

    public bool LoadSceneFromFile(string path, out Scene? scene) =>
        LoadSceneFromFile(path, PostProcessSteps.Triangulate |
                                PostProcessSteps.JoinIdenticalVertices |
                                PostProcessSteps.FindInvalidData |
                                PostProcessSteps.GenerateSmoothNormals |
                                PostProcessSteps.ImproveCacheLocality |
                                PostProcessSteps.FixInFacingNormals |
                                PostProcessSteps.ValidateDataStructure,
                          out scene);

    public unsafe bool LoadSceneFromFile(string path, PostProcessSteps postProcessFlags, out Scene? scene)
    {
        var impScene = _assimp.ImportFile(path, (uint)postProcessFlags);

        if (impScene is null)
        {
            var err = _assimp.GetErrorStringS();
            Log.Error("<Assimp> Failed to import file {FilePath}, Error = {AssimpError}", path, err);

            scene = null;
            return false;
        }

        var meshes = new Mesh[impScene->MNumMeshes];
        for (var i = 0; i < impScene->MNumMeshes; i++)
        {
            var mesh = impScene->MMeshes[i];

            meshes[i] = LoadMesh(*mesh);
        }

        var nodeRoot = *impScene->MRootNode;
        var sceneRoot = new SceneObject(nodeRoot.MName, Matrix4x4.Transpose(nodeRoot.MTransformation),
                                        GetMeshesFromNode(nodeRoot, meshes));

        for (var i = 0; i < nodeRoot.MNumChildren; i++)
        {
            CopyNodesWithMeshes(*nodeRoot.MChildren[i], sceneRoot, Matrix4x4.Transpose(nodeRoot.MTransformation),
                                meshes);
        }

        _assimp.ReleaseImport(impScene);

        scene = new(meshes, sceneRoot);
        return true;
    }

    private static unsafe Mesh LoadMesh(in Silk.NET.Assimp.Mesh mesh)
    {
        var vertCount = (int)mesh.MNumVertices;
        var vertPositions = new ReadOnlySpan<Vector3>(mesh.MVertices, vertCount);
        var normals = new ReadOnlySpan<Vector3>(mesh.MNormals, vertCount);
        var faces = new ReadOnlySpan<Face>(mesh.MFaces, (int)mesh.MNumFaces);

        var colors = new ReadOnlySpan<Vector4>();
        for (var j = 0; j < Assimp.MaxNumberOfColorSets; j++)
        {
            var colorSet = mesh.MColors[j];
            if (colorSet is null) continue;

            colors = new(colorSet, vertCount);
            break;
        }

        if (colors.IsEmpty)
        {
            var defaultColors = new Vector4[vertPositions.Length];
            Array.Fill(defaultColors, Color.White.ToVector4());
            colors = defaultColors;
        }


        var vertices = new List<Vertex>(faces.Length * 3);
        foreach (ref readonly var face in faces)
        {
            if (face.MNumIndices != 3)
            {
                Log.Warning("Mesh {Name} has a face with {NberVertices}, this is not supported. Face was skipped...",
                            mesh.MName, face.MNumIndices);
                continue;
            }

            // Only 3 indices because meshes are triangulated
            for (var j = 0; j < 3; j++)
            {
                var vertex = face.MIndices[j];

                vertices.Add(new(vertPositions[(int)vertex], normals[(int)vertex], colors[(int)vertex]));
            }
        }

        return new(vertices);
    }

    private static unsafe void CopyNodesWithMeshes(in Node node, SceneObject targetParent, Matrix4x4 accTransform,
                                                   IReadOnlyList<Mesh> loadedMeshes)
    {
        SceneObject parent;
        Matrix4x4 transform;

        var nodeTransformAcc = accTransform * Matrix4x4.Transpose(node.MTransformation);

        // If node has meshes, create a new scene object for it
        if (node.MNumMeshes > 0)
        {
            var newObject = new SceneObject(node.MName.AsString, nodeTransformAcc,
                                            GetMeshesFromNode(node, loadedMeshes));

            targetParent.AddChild(newObject);

            parent = newObject;
            transform = Matrix4x4.Identity;
        }
        else
        {
            parent = targetParent;
            transform = nodeTransformAcc;
        }

        for (var i = 0; i < node.MNumChildren; i++)
        {
            CopyNodesWithMeshes(*node.MChildren[i], parent, transform, loadedMeshes);
        }
    }

    private static unsafe IReadOnlyList<Mesh> GetMeshesFromNode(in Node node, IReadOnlyList<Mesh> loadedMeshes)
    {
        var meshIndexes = new ReadOnlySpan<uint>(node.MMeshes, (int)node.MNumMeshes).Cast<uint, int>();
        var meshList = new List<Mesh>((int)node.MNumMeshes);

        foreach (var index in meshIndexes)
            meshList.Add(loadedMeshes[index]);

        return meshList;
    }

    public void Dispose()
    {
        _assimp.Dispose();

        GC.SuppressFinalize(this);
    }
}