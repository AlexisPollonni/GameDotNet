using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan.MemoryAllocation.Defragmentation
{
    internal class BlockListDefragmentationContext
    {
        public Result Result;
        public bool MutexLocked;

        public readonly List<BlockDefragmentationContext> blockContexts = new();
        public readonly List<DefragmentationMove> DefragMoves = new();

        public int DefragMovesProcessed, DefragMovedCommitted;
        public bool HasDefragmentationPlanned;


        public BlockListDefragmentationContext(VulkanMemoryAllocator allocator, VulkanMemoryPool? customPool,
                                               BlockList list, uint currentFrame)
        { }

        public VulkanMemoryPool? CustomPool { get; }

        public BlockList BlockList { get; }

        public DefragmentationAlgorithm Algorithm { get; }
    }
}