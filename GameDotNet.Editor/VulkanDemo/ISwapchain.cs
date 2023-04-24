using System.Collections.Generic;

namespace GameDotNet.Editor.VulkanDemo;

internal interface ISwapchain
{
    public SwapchainImage GetImage(int index);

    public SwapchainImage GetCurrentImage();

    public IReadOnlyList<SwapchainImage> GetImageList();

    public int ImageCount { get; }
    public int CurrentImageIndex { get; }
}