using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using GameDotNet.Core.Tooling;
using GameDotNet.Graphics.ILGPU.Abstractions;
using GameDotNet.Graphics.ILGPU.Models;
using GameDotNet.Graphics.ILGPU.Tools;
using GameDotNet.Graphics.Vulkan.Abstractions;
using GameDotNet.Graphics.Vulkan.Tools.Extensions;
using ILGPU;
using ILGPU.Runtime;
using Microsoft.Extensions.Logging;
using Nito.Disposables;

namespace GameDotNet.Graphics.ILGPU.Services;

public sealed class RenderHost : SingleDisposable<EmptyStruct>
{
    private Context Context { get; }
    internal Accelerator Accelerator { get; }

    private readonly ILogger<RenderHost> _logger;
    private readonly AcceleratorStream _mainStream;
    private readonly FrameGraph _frameGraph;
    private readonly MandelbrotRenderPass<BgraColor> _mandelbrotRenderPass = new();

    public RenderHost(ILogger<RenderHost> logger, IVulkanContext vulkanContext)
        : base(default)
    {
        _logger = logger;
        var builder = Context.Create().Default().EnableAlgorithms()
#if DEBUG
        .Debug();
#else
        .Release();
#endif
        Context = builder.ToContext();

        // Match the specific device index being used by the Vulkan context
        Accelerator = PickAccelerator(vulkanContext.PhysDevice.DeviceUuid).AddGpuInterop();
        _mainStream = Accelerator.CreateStream();
        _frameGraph = new(Accelerator);
    }

    public async ValueTask DrawFrame(
        ArrayView2D<BgraColor, Stride2D.DenseX> arrayView,
        IExternalSemaphore importedSemaphore
    )
    {
        RebuildFrameGraph(importedSemaphore, arrayView);
        _frameGraph.Execute();

        await _mainStream.SynchronizeAsync();
    }

    private void RebuildFrameGraph(
        IExternalSemaphore semaphore,
        ArrayView2D<BgraColor, Stride2D.DenseX> output
    )
    {
        _frameGraph.Reset();

        _frameGraph.ImportExternalView<
            BgraColor,
            Index2D,
            Stride2D.DenseX,
            ArrayView2D<BgraColor, Stride2D.DenseX>
        >(output, out var outputHandle);

        _frameGraph.AddPass(_mandelbrotRenderPass, new(outputHandle));

        _frameGraph.Compile(semaphore);
    }

    private Accelerator PickAccelerator(Guid uuidToMatchSpan)
    {
        foreach (var contextDevice in Context.Devices)
        {
            var name = contextDevice.Name;
            var uuid = contextDevice.GetUuid();

            if (uuidToMatchSpan != uuid)
                continue;

            _logger.LogInformation("Matched Vulkan device with {Name} ({Uuid})", name, uuid);
            return contextDevice.CreateAccelerator(Context);
        }
        return Context.GetPreferredDevice(false).CreateAccelerator(Context);
    }

    protected override void Dispose(EmptyStruct context)
    {
        _frameGraph.Dispose();
        Accelerator.Dispose();
        Context.Dispose();
    }
}
