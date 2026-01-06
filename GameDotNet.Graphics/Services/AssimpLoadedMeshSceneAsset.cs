using Arch.Core;
using Arch.Core.Extensions;
using Assimp;
using AutoFactories;
using GameDotNet.Core.Abstractions;
using GameDotNet.Core.Components;
using GameDotNet.Core.Tooling.Extensions;
using GameDotNet.Graphics.Models;

namespace GameDotNet.Graphics.Services;

[RegisterTransient]
[AutoFactory]
internal sealed class AssimpLoadedMeshSceneAsset(
    string name,
    Identifiable id,
    IReadOnlyDictionary<string, MetadataProperty> metadata,
    Node rootNode,
    IReadOnlyList<AssimpLoadedMeshPartAsset> meshParts) : ISceneAsset
{
    public string Name => name;
    public Identifiable Identifier => id;
    public IEnumerable<IAsset> SceneAssets => meshParts;


    public Entity CreateEntity(World world)
    {
        //TODO: Implement entity creation from scene graph

        var nodeStack = new Stack<(Node node, Entity parentEntity)>();

        var rootEntity = world.Create();
        rootEntity.Add(Label.From(name), id);
        nodeStack.Push((rootNode, rootEntity));

        while (nodeStack.Count > 0)
        {
            var (node, parentEntity) = nodeStack.Pop();

            var entity = world.Create();

            var transform = ComputeWorldTransform(node);

            entity.Add(transform);

            entity.SetParent(parentEntity);


            foreach (var meshIndex in node.MeshIndices)
            {
                var meshPart = meshParts[meshIndex];

                var meshEntity = meshPart.CreateEntity(world);
                //TODO: add mesh component
            }

            foreach (var child in node.Children)
            {
                nodeStack.Push((child, entity));
            }
        }
        
        return rootEntity;
    }

    private Transform ComputeWorldTransform(Node node)
    {
        var transform = node.Transform;
        var parent = node.Parent;

        while (parent is not null)
        {
            transform = parent.Transform * transform;
            parent = parent.Parent;
        }

        return new(transform);
    }
}

public readonly struct GeometryData : ISceneComponent
{
    public IReadOnlyList<Vertex> Vertices { get; init; }
    public IReadOnlyList<uint> Indices { get; init; }
}