using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using GameDotNet.Core.Tooling;
using GameDotNet.Graphics.Vulkan.Abstractions;
using GameDotNet.Graphics.Vulkan.Tools.Extensions;
using Nito.Disposables;
using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan.Wrappers;

public sealed class VulkanCommandBufferPool
    : SingleDisposable<EmptyStruct>,
        IVulkanWrapper<CommandPool>
{
    public IVulkanContext Context { get; }
    public uint QueueFamilyIndex { get; }
    public CommandPool Underlying { get; }

    private readonly List<VulkanCommandBuffer> _primaryBuffers = [];
    private readonly List<VulkanCommandBuffer> _secondaryBuffers = [];

    private int _primaryIndex;
    private int _secondaryIndex;

    public VulkanCommandBufferPool(IVulkanContext context, uint queueFamilyIndex)
        : base(default)
    {
        Context = context;
        QueueFamilyIndex = queueFamilyIndex;

        var commandPoolCreateInfo = new CommandPoolCreateInfo
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = CommandPoolCreateFlags.TransientBit,
            QueueFamilyIndex = queueFamilyIndex,
        };

        context
            .Api.CreateCommandPool(
                context.Device,
                in commandPoolCreateInfo,
                in context.Callbacks.Handle,
                out var pool
            )
            .ThrowOnError();

        Underlying = pool;
    }

    protected override void Dispose(EmptyStruct context)
    {
        Context.Api.DestroyCommandPool(Context.Device, Underlying, in Context.Callbacks.Handle);
    }

    public void Reset()
    {
        Context
            .Api.ResetCommandPool(Context.Device, Underlying, CommandPoolResetFlags.None)
            .ThrowOnError();

        _primaryIndex = 0;
        _secondaryIndex = 0;
    }

    public VulkanCommandBuffer AllocateCommandBuffer(
        CommandBufferLevel level = CommandBufferLevel.Primary
    )
    {
        var cache = level == CommandBufferLevel.Primary ? _primaryBuffers : _secondaryBuffers;
        ref var index = ref (
            level == CommandBufferLevel.Primary ? ref _primaryIndex : ref _secondaryIndex
        );

        if (index < cache.Count)
        {
            var cachedBuffer = cache[index++];
            cachedBuffer.ResetState();
            return cachedBuffer;
        }

        var commandBufferAllocateInfo = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = Underlying,
            CommandBufferCount = 1,
            Level = level,
        };

        Context
            .Api.AllocateCommandBuffers(
                Context.Device,
                in commandBufferAllocateInfo,
                out var commandBuffer
            )
            .ThrowOnError();

        var newBuffer = new VulkanCommandBuffer(Context, commandBuffer, this);
        cache.Add(newBuffer);
        index++;

        return newBuffer;
    }
}
