using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Numerics;
using Assimp;
using CommunityToolkit.HighPerformance;
using dotVariant;
using GameDotNet.Core.Abstractions;
using GameDotNet.Core.Tooling.Extensions;
using GameDotNet.Graphics.Models;
using Shouldly;
using TruePath;
using Metadata = Assimp.Metadata;
using PostProcessPreset = Assimp.PostProcessPreset;

namespace GameDotNet.Graphics.Services;

[RegisterSingleton<IAssetImporter<AssimpLoadedMeshSceneAsset>>]
internal sealed class AssimpNetAssetImporter(
    AssimpLoggerStream logStream,
    IZeroAllocThreadPoolScheduler<AssimpNetAssetImporter.ImportWorkItem> importScheduler,
    IHttpClientFactory httpClientFactory)
    : IAssetImporter<AssimpLoadedMeshSceneAsset>, IDisposable
{
    internal readonly record struct ImportWorkItem(); //TODO: schedule mesh import on thread pool

    private readonly AssimpContext _lib = new();
    private static readonly Guid NamespaceGuid = "gamedotnet.graphics".HashToGuidV5(Guid.NameSpaceDns);

    public async ValueTask<Result<AssimpLoadedMeshSceneAsset>> ImportAsync(AssetSource source,
        CancellationToken token = default)
    {
        try
        {
            var scene = await source.Visit(
                ImportFromFilePath,
                ImportFromEmbeddedResource,
                ImportFromUri,
                ImportFromStream);

            return LoadAssetFromScene(scene);
        }
        catch (Exception e)
        {
            return Result.Failure<AssimpLoadedMeshSceneAsset>(e);
        }


        ValueTask<Scene> ImportFromFilePath(LocalPath path)
        {
            var absolutePath = path.ResolveToCurrentDirectory();

            var fileStream = File.OpenRead(absolutePath.ToString());

            return LoadSceneFromStream(fileStream, path.GetExtensionWithDot(), PostProcessPreset.TargetRealTimeFast,
                token);
        }

        ValueTask<Scene> ImportFromEmbeddedResource(EmbeddedResource resource)
        {
            var resourceInfo = resource.SourceAssembly.GetManifestResourceInfo(resource.ResourceName);

            var stream = resource.SourceAssembly.GetManifestResourceStream(resource.ResourceName);

            if (stream is null)
                throw new InvalidOperationException(
                    $"Resource '{resource.ResourceName}' not found in assembly '{resource.SourceAssembly.FullName}'");

            var hint = new LocalPath(resource.ResourceName).GetExtensionWithDot();
            return LoadSceneFromStream(stream, hint, PostProcessPreset.TargetRealTimeFast, token);
        }

        async ValueTask<Scene> ImportFromUri(Uri uri)
        {
            uri.IsFile.ShouldBeTrue($"Uri source must be a file uri, but '{uri}' is not.");

            var fileExtension = new LocalPath(uri.Segments.Last()).GetExtensionWithDot();

            using var httpClient = httpClientFactory.CreateClient();

            var responseStream = await httpClient.GetStreamAsync(uri, token);

            return await LoadSceneFromStream(responseStream, fileExtension, PostProcessPreset.TargetRealTimeFast,
                token);
        }

        ValueTask<Scene> ImportFromStream(Stream stream) => LoadSceneFromStream(stream,
            postProcess: PostProcessPreset.TargetRealTimeFast, token: token);
    }


    private async ValueTask<Scene> LoadSceneFromStream(Stream stream,
        string? formatHint = null, PostProcessSteps postProcess = PostProcessSteps.None,
        CancellationToken token = default)
    {
        if (stream.CanSeek)
        {
            return _lib.ImportFileFromStream(stream, postProcess, formatHint);
        }

        using var memoryStream = new MemoryStream();

        await stream.CopyToAsync(memoryStream, token);

        return _lib.ImportFileFromStream(memoryStream, postProcess, formatHint);
    }

    private AssimpLoadedMeshSceneAsset LoadAssetFromScene(Scene scene)
    {
        var metadata = GetMetadata(scene.Metadata);
        var meshes = scene.Meshes.Select(ImportMeshPart).ToArray();
        //TODO: here import other types of assets like materials, animations, etc.

        return new(scene.RootNode, meshes, scene.Name, scene.Name.ToIdentifiable(NamespaceGuid), metadata);
    }

    public void Dispose()
    {
        logStream.Dispose();
        _lib.Dispose();
    }

    private AssimpLoadedMeshPartAsset ImportMeshPart(Mesh mesh)
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
                var colorSet = mesh.VertexColorChannels[i].AsSpan();

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

        var meshId = $"{mesh.Name}".ToIdentifiable(NamespaceGuid);
        return new(mesh.Name, meshId, vertices, mesh.GetUnsignedIndices().ToArray(), null);
    }


    private static IReadOnlyDictionary<string, MetadataProperty> GetMetadata(Metadata data)
    {
        var result = new Dictionary<string, MetadataProperty>(data.Count);

        foreach (var (key, entry) in data)
        {
            MetadataProperty prop = entry.DataType switch
            {
                MetaDataType.Bool => new(entry.DataAs<bool>().GetValueOrDefault()),
                MetaDataType.Int32 => new(entry.DataAs<int>().GetValueOrDefault()),
                MetaDataType.Int64 => new(entry.DataAs<long>().GetValueOrDefault()),
                MetaDataType.UInt32 => new(entry.DataAs<uint>().GetValueOrDefault()),
                MetaDataType.UInt64 => new(entry.DataAs<ulong>().GetValueOrDefault()),
                MetaDataType.Float => new(entry.DataAs<float>().GetValueOrDefault()),
                MetaDataType.Double => new(entry.DataAs<double>().GetValueOrDefault()),
                MetaDataType.String => new(entry.Data as string ?? string.Empty),
                MetaDataType.Vector3 => new(entry.DataAs<Vector3>().GetValueOrDefault()),
                _ => throw new ArgumentOutOfRangeException(nameof(entry.DataType),
                    "Metadata entry type is out of range")
            };

            result.Add(key, prop);
        }

        return result;
    }
}

[Variant]
[SuppressMessage("ReSharper", "PartialMethodWithSinglePart")]
public partial class MetadataProperty
{
    static partial void VariantOf(bool a,
        int b,
        long c,
        uint d,
        ulong e,
        float f,
        double g,
        string h,
        Vector3 i,
        Dictionary<string, MetadataProperty> j);
}