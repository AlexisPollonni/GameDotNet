using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using GameDotNet.Core.Tooling;
using GameDotNet.Graphics.Vulkan.Abstractions;
using GameDotNet.Graphics.Vulkan.Tools.Extensions;
using Nito.Disposables;
using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace GameDotNet.Graphics.Vulkan.Wrappers;

public class VulkanSemaphore
    : SingleDisposable<IVulkanContext>,
        IVulkanWrapper<Semaphore>,
        IVulkanCreateFromInfo<SemaphoreCreateInfo>
{
    public IVulkanContext Context { get; }
    public Semaphore Underlying => _lazySemaphore.Value.Item2;
    public IChain<SemaphoreCreateInfo> InfoChain => _lazySemaphore.Value.Item1;

    private readonly Lazy<(IChain<SemaphoreCreateInfo>, Semaphore)> _lazySemaphore;

    public VulkanSemaphore(IVulkanContext context)
        : base(context)
    {
        Context = context;

        _lazySemaphore = new(SemaphoreFactory, false);
    }

    private (IChain<SemaphoreCreateInfo>, Semaphore) SemaphoreFactory()
    {
        var infoChain = CreateVulkanInfo();
        Context
            .Api.CreateSemaphore(
                Context.Device,
                in infoChain.HeadRef,
                in Context.Callbacks.Underlying,
                out var createdSem
            )
            .ThrowOnError("Unable to create semaphore");

        return (infoChain, createdSem);
    }

    public virtual unsafe IChain<SemaphoreCreateInfo> CreateVulkanInfo() =>
        Chain.Create(new SemaphoreCreateInfo(flags: SemaphoreCreateFlags.None));

    public static implicit operator Semaphore(VulkanSemaphore s) => s.Underlying;

    protected sealed override void Dispose(IVulkanContext context)
    {
        if (!_lazySemaphore.IsValueCreated)
            return;
        InfoChain.Dispose();
        Context.Api.DestroySemaphore(context.Device, Underlying, in context.Callbacks.Underlying);
    }
}

public class VulkanTimelineSemaphore(IVulkanContext context) : VulkanSemaphore(context)
{
    public override unsafe IChain<SemaphoreCreateInfo> CreateVulkanInfo() =>
        Chain.Create(
            new SemaphoreCreateInfo(flags: SemaphoreCreateFlags.None),
            new SemaphoreTypeCreateInfo(initialValue: 0, semaphoreType: SemaphoreType.Timeline)
        );

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

public sealed class ExportableVulkanTimelineSemaphore(
    IVulkanContext context,
    ExternalSemaphoreHandleTypeFlags handleTypes
) : VulkanTimelineSemaphore(context)
{
    public override unsafe IChain<SemaphoreCreateInfo> CreateVulkanInfo() =>
        Chain.Create(
            new SemaphoreCreateInfo(flags: SemaphoreCreateFlags.None),
            new SemaphoreTypeCreateInfo(initialValue: 0, semaphoreType: SemaphoreType.Timeline),
            new ExportSemaphoreCreateInfo(handleTypes: handleTypes)
        );
}
