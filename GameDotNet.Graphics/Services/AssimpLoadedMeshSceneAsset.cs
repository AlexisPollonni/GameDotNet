using Arch.Core;
using Arch.Core.Extensions;
using Assimp;
using AutoCtor;
using GameDotNet.Core.Abstractions;
using GameDotNet.Core.Components;
using GameDotNet.Core.Tooling.Extensions;
using GameDotNet.Graphics.Models;

namespace GameDotNet.Graphics.Services;

[AutoConstruct]
internal sealed partial class AssimpLoadedMeshSceneAsset : ISceneAsset
{
    public string Name { get; }
    public Identifiable Identifier { get; }
    public IEnumerable<IAsset> SceneAssets => _meshParts;
    
    public IReadOnlyDictionary<string, MetadataProperty> Metadata { get; }

    private readonly Node _rootNode;
    private readonly IReadOnlyList<AssimpLoadedMeshPartAsset> _meshParts;

    public Entity CreateEntity(World world)
    {
        //TODO: Implement entity creation from scene graph

        var nodeStack = new Stack<(Node node, Entity parentEntity)>();

        var rootEntity = world.Create();
        rootEntity.Add(Label.From(Name), Identifier);
        nodeStack.Push((_rootNode, rootEntity));

        while (nodeStack.Count > 0)
        {
            var (node, parentEntity) = nodeStack.Pop();

            var entity = world.Create();

            var transform = ComputeWorldTransform(node);

            entity.Add(transform);

            entity.SetParent(parentEntity);


            foreach (var meshIndex in node.MeshIndices)
            {
                var meshPart = _meshParts[meshIndex];

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