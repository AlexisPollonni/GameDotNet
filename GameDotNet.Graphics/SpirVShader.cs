using CommunityToolkit.HighPerformance;
using GameDotNet.Core.Tools.Extensions;
using GameDotNet.Graphics.Abstractions;
using Silk.NET.SPIRV.Reflect;
using SpirvReflectSharp;

namespace GameDotNet.Graphics;

public sealed class SpirVShader : IShader
{
    public ShaderDescription Description { get; }
    public uint[] Code { get; }


    private readonly ShaderModule _reflectModule;
    
    public SpirVShader(ReadOnlySpan<uint> code, ShaderDescription description)
    {
        Code = code.ToArray();
        Description = description;

        _reflectModule = SpirvReflect.ReflectCreateShaderModule(code.AsBytes());
    }

    public Task SaveToFile(string path, CancellationToken token = default) 
        => File.WriteAllBytesAsync(path, Code.AsSpan().AsBytes().ToArray(),token);

    public IOrderedEnumerable<UniformEntry> GetUniforms()
    {
        var uniforms = new List<UniformEntry>(); 
        var entryPoint = _reflectModule.EntryPoints.Single();

        foreach (var set in entryPoint.DescriptorSets)
        {
            foreach (var binding in set.Bindings)
            {
                if(binding.DescriptorType is not DescriptorType.UniformBuffer)
                    continue;

                var entry = new UniformEntry(binding.Name,
                                             ToShaderStage(entryPoint.ShaderStage),
                                             binding.Set,
                                             binding.Binding,
                                             GetMinByteSizeFromTypeDef(binding.TypeDescription), false);
                uniforms.Add(entry);
            }
        }

        return uniforms.OrderBy(entry => entry.Set)
                .ThenBy(entry => entry.Binding);
    }

    private static ShaderStage ToShaderStage(ShaderStageFlagBits stage)
    {
        return stage switch
        {
            ShaderStageFlagBits.VertexBit => ShaderStage.Vertex,
            ShaderStageFlagBits.FragmentBit => ShaderStage.Fragment,
            ShaderStageFlagBits.ComputeBit => ShaderStage.Compute,
            _ => throw new ArgumentOutOfRangeException(nameof(stage),
                                                       "SpirV shader execution model is not supported")
        };
    }

    private static ulong GetMinByteSizeFromTypeDef(ReflectTypeDescription type)
    {
        var flags = type.TypeFlags;
        if (flags.Has(TypeFlagBits.Array))
        {
            return type.Traits.Array.Stride;
        }
        if (flags.Has(TypeFlagBits.Struct))
        {
            return type.Members.Aggregate(0ul, (current, m) => current + GetMinByteSizeFromTypeDef(m));
        }
        if (flags.Has(TypeFlagBits.Matrix))
        {
            return type.Traits.Numeric.Matrix.Stride * type.Traits.Numeric.Matrix.RowCount;
        }

        if (flags.Has(TypeFlagBits.Vector))
        {
            return type.Traits.Numeric.Vector.ComponentCount * type.Traits.Numeric.Scalar.Width;
        }
        
        return type.Traits.Numeric.Scalar.Width;
    }


    public void Dispose()
    {
        _reflectModule.Dispose();
    }
}

public class ShaderEntry(string name, ShaderStage stage, uint set, uint binding)
{
    public string Name { get; } = name;
    public ShaderStage Stage { get; } = stage;
    public uint Set { get; } = set;
    public uint Binding { get; } = binding;
}

public class UniformEntry(string name, ShaderStage stage, uint set, uint binding, ulong minSize, bool isDynamic) : ShaderEntry(name, stage, set, binding)
{
    public ulong MinSize { get; } = minSize;
    public bool IsDynamic { get; set; } = isDynamic;
}

