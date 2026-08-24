using System.Diagnostics;
using GameDotNet.Core.Tooling;
using GameDotNet.Core.Tooling.Extensions;
using GameDotNet.Graphics.Abstractions;
using GameDotNet.Graphics.ILGPU.Models;
using GameDotNet.Graphics.Tooling;
using GameDotNet.Graphics.Vulkan.Services;
using GameDotNet.Graphics.Vulkan.Tools.Extensions;
using GameDotNet.Graphics.Vulkan.Wrappers;
using ILGPU.Runtime;
using ManagedCuda;
using Microsoft.Extensions.DependencyInjection;
using Nito.Disposables;
using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.ILGPU.Services;

internal sealed class EntityRenderer(
    [FromKeyedServices(QueueFlags.GraphicsBit)] CommandSubmitter submitter,
    VulkanInteropExporter vkExporter,
    RenderHost host,
    TimeProvider provider
) : SingleDisposable<EmptyStruct>(default), IEntityRenderer
{
    private ImportedVulkanBuffer? _importedBuffer;
    private ImportedVulkanSemaphore? _importedSemaphore;
    private readonly TimingsRingBuffer _timings = new(100);

    public async ValueTask<TimelineStats> Render(IDeviceTexture renderTarget)
    {
        var startTimestamp = provider.GetTimestamp();

        if (renderTarget is not VulkanImage vkImage)
        {
            throw new ArgumentException($"{renderTarget} is not a vulkan image");
        }

        if (vkImage.Size.Bits <= 0L)
        {
            _timings.Add(provider.GetElapsedTime(startTimestamp));
            return _timings.ComputeStats();
        }

        if (_importedBuffer is null || _importedBuffer.Size < vkImage.Size)
        {
            _importedBuffer?.Dispose();
            _importedBuffer = new(host.Accelerator, vkExporter, vkImage.Size);
        }

        _importedSemaphore ??= new(host.Accelerator, vkExporter);

        var arrayView = _importedBuffer
            .Buffer.AsRawArrayView()
            .Cast<BgraColor>()
            .AsDense()
            .As2DDenseXView(new(vkImage.Extent.Width, vkImage.Extent.Height));

        await host.DrawFrame(arrayView, _importedSemaphore.ImportedSemaphore);
        var batch = submitter.CreateBatch();
        {
            using var recorder = submitter.CreateRecorder(batch);
            recorder
                .CopyBufferToImage(_importedBuffer.VkBuffer, vkImage)
                .TransitionLayout(vkImage, ImageLayout.ShaderReadOnlyOptimal);
        }
        await submitter.SubmitAsync(batch);

        _timings.Add(provider.GetElapsedTime(startTimestamp));
        return _timings.ComputeStats();
    }

    protected override void Dispose(EmptyStruct context)
    {
        _importedBuffer?.Dispose();
        _importedSemaphore?.Dispose();
    }
}
