using GameDotNet.Graphics.Vulkan.Abstractions;
using GameDotNet.Graphics.Vulkan.Tools.Extensions;
using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan.Wrappers;

[Flags]
public enum VulkanShaderStages : uint
{
    None = 0,
    Vertex = 1 << 0,
    Fragment = 1 << 1,
    Compute = 1 << 2,
    Geometry = 1 << 3,
    TessControl = 1 << 4,
    TessEvaluation = 1 << 5,
}

public enum VulkanDescriptorType
{
    UniformBuffer,
    StorageBuffer,
    SampledImage,
    StorageImage,
    Sampler,
}

public readonly record struct VulkanDescriptorBinding(
    uint Set,
    uint Binding,
    VulkanDescriptorType Type,
    uint Count,
    VulkanShaderStages Stages,
    bool IsBindless = false
);

public readonly record struct VulkanPushConstantRangeDesc(
    uint Offset,
    uint Size,
    VulkanShaderStages Stages
);

public sealed class VulkanPipelineLayout : IVulkanWrapper<PipelineLayout>, IDisposable
{
    public IVulkanContext Context { get; }
    public PipelineLayout Underlying { get; }

    private readonly DescriptorSetLayout[] _descriptorSetLayouts;
    private bool _disposed;

    internal VulkanPipelineLayout(
        IVulkanContext context,
        PipelineLayout underlying,
        DescriptorSetLayout[] descriptorSetLayouts
    )
    {
        Context = context;
        Underlying = underlying;
        _descriptorSetLayouts = descriptorSetLayouts;
    }

    public static implicit operator PipelineLayout(VulkanPipelineLayout layout) =>
        layout.Underlying;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        var api = Context.Api;
        var device = Context.Device.Underlying;

        foreach (var descriptorSetLayout in _descriptorSetLayouts)
        {
            api.DestroyDescriptorSetLayout(
                device,
                descriptorSetLayout,
                in Context.Callbacks.Underlying
            );
        }

        api.DestroyPipelineLayout(device, Underlying, in Context.Callbacks.Underlying);
        GC.SuppressFinalize(this);
    }
}

public static class VulkanPipelineLayoutFactory
{
    public static unsafe VulkanPipelineLayout Create(
        VulkanDevice device,
        ReadOnlySpan<VulkanDescriptorBinding> bindings,
        ReadOnlySpan<VulkanPushConstantRangeDesc> pushConstantRanges
    )
    {
        var context = device.Context;
        var api = context.Api;
        var nativeDevice = device.Underlying;

        var maxSet = -1;
        for (var i = 0; i < bindings.Length; i++)
        {
            maxSet = Math.Max(maxSet, (int)bindings[i].Set);
        }

        var setLayoutCount = Math.Max(0, maxSet + 1);
        var setLayouts = new DescriptorSetLayout[setLayoutCount];

        for (var set = 0; set < setLayoutCount; set++)
        {
            var bindingCount = 0;
            for (var i = 0; i < bindings.Length; i++)
            {
                if (bindings[i].Set == (uint)set)
                    bindingCount++;
            }

            var bindingsInSet = new VulkanDescriptorBinding[bindingCount];
            var copyIndex = 0;
            for (var i = 0; i < bindings.Length; i++)
            {
                if (bindings[i].Set == (uint)set)
                    bindingsInSet[copyIndex++] = bindings[i];
            }

            setLayouts[set] = CreateDescriptorSetLayout(context, bindingsInSet);
        }

        var nativePushConstantRanges = new PushConstantRange[pushConstantRanges.Length];
        for (var i = 0; i < pushConstantRanges.Length; i++)
        {
            var range = pushConstantRanges[i];
            nativePushConstantRanges[i] = new PushConstantRange
            {
                Offset = range.Offset,
                Size = range.Size,
                StageFlags = ToNativeShaderStages(range.Stages),
            };
        }

        fixed (DescriptorSetLayout* setLayoutsPtr = setLayouts)
        fixed (PushConstantRange* pushConstantRangesPtr = nativePushConstantRanges)
        {
            var pipelineLayoutCreateInfo = new PipelineLayoutCreateInfo
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = (uint)setLayouts.Length,
                PSetLayouts = setLayouts.Length == 0 ? null : setLayoutsPtr,
                PushConstantRangeCount = (uint)nativePushConstantRanges.Length,
                PPushConstantRanges =
                    nativePushConstantRanges.Length == 0 ? null : pushConstantRangesPtr,
            };

            api.CreatePipelineLayout(
                    nativeDevice,
                    in pipelineLayoutCreateInfo,
                    in context.Callbacks.Underlying,
                    out var nativePipelineLayout
                )
                .ThrowOnError("Failed to create Vulkan pipeline layout.");

            return new VulkanPipelineLayout(context, nativePipelineLayout, setLayouts);
        }
    }

    private static unsafe DescriptorSetLayout CreateDescriptorSetLayout(
        IVulkanContext vulkanContext,
        ReadOnlySpan<VulkanDescriptorBinding> bindings
    )
    {
        var nativeBindings =
            bindings.Length == 0 ? [] : stackalloc DescriptorSetLayoutBinding[bindings.Length];
        for (var i = 0; i < bindings.Length; i++)
        {
            var binding = bindings[i];
            nativeBindings[i] = new DescriptorSetLayoutBinding
            {
                Binding = binding.Binding,
                DescriptorType = ToNativeDescriptorType(binding.Type),
                DescriptorCount = Math.Max(1u, binding.Count),
                StageFlags = ToNativeShaderStages(binding.Stages),
            };
        }

        fixed (DescriptorSetLayoutBinding* bindingsPtr = nativeBindings)
        {
            var descriptorSetLayoutCreateInfo = new DescriptorSetLayoutCreateInfo
            {
                SType = StructureType.DescriptorSetLayoutCreateInfo,
                BindingCount = (uint)nativeBindings.Length,
                PBindings = nativeBindings.Length == 0 ? null : bindingsPtr,
            };

            vulkanContext
                .Api.CreateDescriptorSetLayout(
                    vulkanContext.Device,
                    in descriptorSetLayoutCreateInfo,
                    in vulkanContext.Callbacks.Underlying,
                    out var descriptorSetLayout
                )
                .ThrowOnError("Failed to create Vulkan descriptor set layout.");

            return descriptorSetLayout;
        }
    }

    private static DescriptorType ToNativeDescriptorType(VulkanDescriptorType type) =>
        type switch
        {
            VulkanDescriptorType.UniformBuffer => DescriptorType.UniformBuffer,
            VulkanDescriptorType.StorageBuffer => DescriptorType.StorageBuffer,
            VulkanDescriptorType.SampledImage => DescriptorType.SampledImage,
            VulkanDescriptorType.StorageImage => DescriptorType.StorageImage,
            VulkanDescriptorType.Sampler => DescriptorType.Sampler,
            _ => DescriptorType.UniformBuffer,
        };

    private static ShaderStageFlags ToNativeShaderStages(VulkanShaderStages stages)
    {
        var native = ShaderStageFlags.None;
        if ((stages & VulkanShaderStages.Vertex) != 0)
            native |= ShaderStageFlags.VertexBit;
        if ((stages & VulkanShaderStages.Fragment) != 0)
            native |= ShaderStageFlags.FragmentBit;
        if ((stages & VulkanShaderStages.Compute) != 0)
            native |= ShaderStageFlags.ComputeBit;
        if ((stages & VulkanShaderStages.Geometry) != 0)
            native |= ShaderStageFlags.GeometryBit;
        if ((stages & VulkanShaderStages.TessControl) != 0)
            native |= ShaderStageFlags.TessellationControlBit;
        if ((stages & VulkanShaderStages.TessEvaluation) != 0)
            native |= ShaderStageFlags.TessellationEvaluationBit;
        return native;
    }
}
