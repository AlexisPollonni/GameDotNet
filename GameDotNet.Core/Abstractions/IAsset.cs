using System.Reflection;
using Arch.Core;
using dotVariant;
using GameDotNet.Core.Components;
using TruePath;

namespace GameDotNet.Core.Abstractions;

public interface IAsset
{
    string Name { get; }
    Identifiable Identifier { get; }
    
    Entity CreateEntity(World world);
}


[Variant]
public readonly partial struct AssetSource
{
    static partial void VariantOf(LocalPath filePath,
                                  EmbeddedResource embedded,
                                  Uri webUri,
                                  Stream dataStream);
}

public readonly record struct EmbeddedResource(Assembly SourceAssembly, string ResourceName);