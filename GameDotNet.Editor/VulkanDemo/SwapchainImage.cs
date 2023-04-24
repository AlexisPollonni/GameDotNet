using System.Drawing;
using Silk.NET.Vulkan;
using Image = Silk.NET.Vulkan.Image;

namespace GameDotNet.Editor.VulkanDemo;

internal readonly record struct SwapchainImage(Format Format, Size Size, Image Handle, ImageLayout Layout,
                                               ImageTiling Tiling, ImageUsageFlags UsageFlags, uint LevelCount,
                                               uint SampleCount, DeviceMemory MemoryHandle, ImageView ViewHandle,
                                               ulong MemorySize, bool IsProtected);