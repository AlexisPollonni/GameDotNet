using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using Avalonia.Logging;
using Avalonia.Vulkan;
using Silk.NET.Vulkan;
using Image = Silk.NET.Vulkan.Image;

namespace GameDotNet.Editor.VulkanDemo;

public class VulkanDemoControl : VulkanControlBase
{
    private VulkanResources? _resources;

    protected override void OnVulkanInit(IVulkanSharedDevice sharedDevice)
    {
        using var l = sharedDevice.Device.Lock();

        var res = VulkanContext.TryCreate(sharedDevice);
        if (res.context is null)
        {
            Logger.TryGet(LogEventLevel.Error, "Vulkan")
                  ?.Log(this, "Couldn't initialize vulkan context: {Info}", res.info);
            return;
        }

        _resources = new(res.context, new(res.context));
    }

    protected override void OnVulkanDeInit(IVulkanSharedDevice device)
    {
        _resources?.Dispose();
        _resources = null;
    }

    protected override void OnVulkanRender(IVulkanSharedDevice device, ISwapchain image)
    {
        using var l = device.Device.Lock();

        _resources?.Content.Render(new AvaloniaSwapchain(image));
    }

    private class VulkanResources : IDisposable
    {
        public VulkanContext Context { get; }
        public VulkanContent Content { get; }

        public VulkanResources(VulkanContext context, VulkanContent content)
        {
            Context = context;
            Content = content;
        }

        public void Dispose()
        {
            Content.Dispose();
            Context.Dispose();
        }
    }

    private class AvaloniaSwapchain : GameDotNet.Editor.VulkanDemo.ISwapchain
    {
        public IReadOnlyList<SwapchainImage> Images { get; }
        public int ImageCount { get; }
        public int CurrentImageIndex { get; }

        public AvaloniaSwapchain(ISwapchain swapchain)
        {
            ImageCount = swapchain.ImageCount;
            CurrentImageIndex = swapchain.CurrentImageIndex;

            var imgs = new SwapchainImage[ImageCount];
            for (var i = 0; i < ImageCount; i++)
            {
                var img = swapchain.GetImage(i);
                ref var img2 = ref Unsafe.As<VulkanImageInfo, SwapchainImage>(ref img);

                imgs[i] = img2;
            }

            Images = imgs;
        }
    }
}

internal interface ISwapchain
{
    public IReadOnlyList<SwapchainImage> Images { get; }

    public int ImageCount { get; }
    public int CurrentImageIndex { get; }
}

internal readonly record struct SwapchainImage(Format Format, Size Size, Image Handle, ImageLayout Layout,
                                               ImageTiling Tiling, ImageUsageFlags UsageFlags, uint LevelCount,
                                               uint SampleCount, DeviceMemory MemoryHandle, ImageView ViewHandle,
                                               ulong MemorySize, bool IsProtected);