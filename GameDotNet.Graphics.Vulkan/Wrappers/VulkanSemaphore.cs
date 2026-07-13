using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using GameDotNet.Core.Tooling;
using GameDotNet.Graphics.Vulkan.Abstractions;
using GameDotNet.Graphics.Vulkan.Tools.Extensions;
using Nito.Disposables;
using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace GameDotNet.Graphics.Vulkan.Wrappers;

public class VulkanSemaphore : SingleDisposable<EmptyStruct>, IVulkanWrapper<Semaphore>
{
    public IVulkanContext Context { get; }
    public Semaphore Underlying { get; }

    public VulkanSemaphore(IVulkanContext context)
        : this(
            context,
            new() { SType = StructureType.SemaphoreCreateInfo, Flags = SemaphoreCreateFlags.None }
        ) { }

    public VulkanSemaphore(IVulkanContext context, in SemaphoreCreateInfo info)
        : base(default)
    {
        Context = context;

        context
            .Api.CreateSemaphore(
                context.Device,
                in info,
                in context.Callbacks.Underlying,
                out var sem
            )
            .ThrowOnError("Unable to create semaphore");

        Underlying = sem;
    }

    public static implicit operator Semaphore(VulkanSemaphore s) => s.Underlying;

    protected sealed override void Dispose(EmptyStruct context)
    {
        Context.Api.DestroySemaphore(Context.Device, Underlying, in Context.Callbacks.Underlying);
    }
}

public class VulkanTimelineSemaphore : VulkanSemaphore
{
    public VulkanTimelineSemaphore(IVulkanContext context)
        : base(context, CreateInfo(out var createChain))
    {
        createChain.Dispose();
    }

    private static unsafe SemaphoreCreateInfo CreateInfo(
        out Chain<SemaphoreCreateInfo, SemaphoreTypeCreateInfo> chain
    )
    {
        chain = Chain.Create(
            new SemaphoreCreateInfo(flags: SemaphoreCreateFlags.None),
            new SemaphoreTypeCreateInfo(initialValue: 0, semaphoreType: SemaphoreType.Timeline)
        );

        return chain.Head;
    }

    public ulong CurrentValue
    {
        get
        {
            Context
                .Api.GetSemaphoreCounterValue(Context.Device, Underlying, out var value)
                .ThrowOnError();
            return value;
        }
    }

    public void Signal(ulong value)
    {
        var semaphoreSignalInfo = new SemaphoreSignalInfo { Semaphore = Underlying, Value = value };

        Context.Api.SignalSemaphore(Context.Device, in semaphoreSignalInfo).ThrowOnError();
    }

    public unsafe void WaitHost(ulong value, ulong timeout = ulong.MaxValue)
    {
        ReadOnlySpan<Semaphore> semaphores = [Underlying];
        ReadOnlySpan<ulong> values = [value];
        fixed (Semaphore* pSem = semaphores)
        fixed (ulong* pVal = values)
        {
            var semaphoreWaitInfo = new SemaphoreWaitInfo
            {
                SType = StructureType.SemaphoreWaitInfo,
                SemaphoreCount = 1,
                PSemaphores = pSem,
                PValues = pVal,
                Flags = SemaphoreWaitFlags.AnyBit,
            };

            Context
                .Api.WaitSemaphores(Context.Device, in semaphoreWaitInfo, timeout)
                .ThrowOnError();
        }
    }

    public static unsafe Result WaitAny(
        ReadOnlySpan<VulkanTimelineSemaphore> semaphores,
        ReadOnlySpan<ulong> values,
        TimeSpan timeout
    )
    {
        if (semaphores.Length != values.Length)
            throw new ArgumentException("Semaphore and value spans must be the same length");

        var context = semaphores[0].Context; // we assume all semaphores are from the same context

        var pSem = stackalloc Semaphore[semaphores.Length];
        for (var i = 0; i < semaphores.Length; i++)
            pSem[i] = semaphores[i].Underlying;

        fixed (ulong* pVal = values)
        {
            var semaphoreWaitInfo = new SemaphoreWaitInfo
            {
                SType = StructureType.SemaphoreWaitInfo,
                SemaphoreCount = (uint)semaphores.Length,
                PSemaphores = pSem,
                PValues = pVal,
                Flags = SemaphoreWaitFlags.AnyBit,
            };

            return context.Api.WaitSemaphores(
                context.Device,
                in semaphoreWaitInfo,
                (ulong)timeout.TotalNanoseconds
            );
        }
    }
}
