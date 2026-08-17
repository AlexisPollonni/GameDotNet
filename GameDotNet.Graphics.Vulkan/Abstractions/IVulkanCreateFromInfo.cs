using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan.Abstractions;

public interface IVulkanCreateFromInfo<TInfo>
    where TInfo : IChainStart
{
    internal IChain<TInfo> InfoChain { get; }

    protected IChain<TInfo> CreateVulkanInfo();
}
