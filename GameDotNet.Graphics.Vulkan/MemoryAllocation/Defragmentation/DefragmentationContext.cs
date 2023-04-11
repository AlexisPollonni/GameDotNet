using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan.MemoryAllocation.Defragmentation
{
    public sealed class DefragmentationContext : IDisposable
    {
        private readonly VulkanMemoryAllocator _allocator;
        private readonly uint _currentFrame;
        private readonly uint _flags;
        private DefragmentationStats _stats;

        private ulong _maxCpuBytesToMove, _maxGpuBytesToMove;
        private int _maxCpuAllocationsToMove, _maxGpuAllocationsToMove;

        private readonly BlockListDefragmentationContext[] _defaultPoolContexts =
            new BlockListDefragmentationContext[Vk.MaxMemoryTypes];

        private readonly List<BlockListDefragmentationContext> _customPoolContexts = new();


        internal DefragmentationContext(VulkanMemoryAllocator allocator, uint currentFrame, uint flags,
                                        DefragmentationStats stats)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        internal void AddPools(params VulkanMemoryPool[] pools)
        {
            throw new NotImplementedException();
        }

        internal void AddAllocations(Allocation[] allocations, out bool[] allocationsChanged)
        {
            throw new NotImplementedException();
        }

        internal Result Defragment(ulong maxCpuBytesToMove, int maxCpuAllocationsToMove, ulong maxGpuBytesToMove,
                                   int maxGpuAllocationsToMove, CommandBuffer cbuffer, DefragmentationStats stats,
                                   DefragmentationFlags flags)
        {
            throw new NotImplementedException();
        }

        internal Result DefragmentationPassBegin(ref DefragmentationPassMoveInfo[] info)
        {
            throw new NotImplementedException();
        }

        internal Result DefragmentationPassEnd()
        {
            throw new NotImplementedException();
        }
    }
}