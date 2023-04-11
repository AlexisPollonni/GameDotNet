using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan.Tools;

public class VulkanException : Exception
{
    public VulkanException(Result res) : base($"A vulkan function returned an unhandled error code : {res}")
    { }
}