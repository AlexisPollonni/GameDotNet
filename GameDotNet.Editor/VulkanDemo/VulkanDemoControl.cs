using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Avalonia.Logging;
using Avalonia.Threading;
using Avalonia.Vulkan;

namespace GameDotNet.Editor.VulkanDemo;

public class VulkanDemoControl : VulkanControlBase
{
    private VulkanResources? _resources;

    protected override void OnVulkanInit(IVulkanSharedDevice sharedDevice)
    {
        using var l = sharedDevice.Device.Lock();

        var res = AvaloniaVulkanContext.TryCreate(sharedDevice);
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

        Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
    }


    private class VulkanResources : IDisposable
    {
        public AvaloniaVulkanContext Context { get; }
        public VulkanContent Content { get; }

        public VulkanResources(AvaloniaVulkanContext context, VulkanContent content)
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

    private class AvaloniaSwapchain : GameDotNet.Editor.VulkanDemo.ISwapchain, IEquatable<AvaloniaSwapchain>
    {
        public int ImageCount => _swapchain.ImageCount;

        public int CurrentImageIndex => _swapchain.CurrentImageIndex;


        private readonly ISwapchain _swapchain;

        public AvaloniaSwapchain(ISwapchain swapchain)
        {
            _swapchain = swapchain;
        }

        public SwapchainImage GetImage(int index)
        {
            var img = _swapchain.GetImage(index);
            return Unsafe.As<VulkanImageInfo, SwapchainImage>(ref img);
        }

        public SwapchainImage GetCurrentImage()
        {
            return GetImage(CurrentImageIndex);
        }

        public IReadOnlyList<SwapchainImage> GetImageList()
        {
            var images = new SwapchainImage[ImageCount];
            for (var i = 0; i < ImageCount; i++)
                images[i] = GetImage(i);
            return images;
        }

        public bool Equals(AvaloniaSwapchain? other)
        {
            if (ReferenceEquals(null, other)) return false;
            return ReferenceEquals(this, other) || _swapchain.Equals(other._swapchain);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((AvaloniaSwapchain)obj);
        }

        public override int GetHashCode() => _swapchain.GetHashCode();
    }
}