using Silk.NET.Vulkan;

namespace Core.Graphics.Vulkan;

public class VulkanException: Exception
{
    public VulkanException(Result res) : base($"A vulkan function returned an unhandled error code : {res}")
    {
    }
}