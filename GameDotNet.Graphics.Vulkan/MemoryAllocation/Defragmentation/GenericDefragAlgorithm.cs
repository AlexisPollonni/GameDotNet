using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan.MemoryAllocation.Defragmentation
{
    internal sealed class GenericDefragAlgorithm : DefragmentationAlgorithm
    {
        private int allocationCount;
        private bool allAllocations;

        private ulong bytesMoved;
        private int allocationsMoved;

        public GenericDefragAlgorithm(VulkanMemoryAllocator allocator, BlockList list, uint currentFrame,
                                      bool overlappingMoveSupported) : base(allocator, list, currentFrame)
        { }

        public override ulong BytesMoved => throw new NotImplementedException();

        public override int AllocationsMoved => throw new NotImplementedException();

        public override void AddAll()
        {
            throw new NotImplementedException();
        }

        public override void AddAllocation(Allocation alloc, out bool changed)
        {
            throw new NotImplementedException();
        }

        public override Result Defragment(ulong maxBytesToMove, int maxAllocationsToMove, DefragmentationFlags flags,
                                          out DefragmentationMove[] moves)
        {
            throw new NotImplementedException();
        }

        private class BlockInfo
        {
            public int OriginalBlockIndex;
            public VulkanMemoryBlock Block;
            public bool HasNonMovableAllocations;
            public readonly List<AllocateInfo> Allocations = new();

            public void CalcHasNonMovableAllocations()
            {
                HasNonMovableAllocations = Block.MetaData.AllocationCount != Allocations.Count;
            }

            public void SortAllocationsBySizeDescending()
            {
                Allocations.Sort(delegate(AllocateInfo info1, AllocateInfo info2)
                {
                    return info1.Allocation.Size.CompareTo(info2.Allocation.Size);
                });
            }

            public void SortAllocationsByOffsetDescending()
            {
                Allocations.Sort((info1, info2) => info1.Allocation.Offset.CompareTo(info2.Allocation.Offset));
            }
        }

        private readonly List<BlockInfo> _blocks = new();

        //private Result DefragmentRound()

        private int CalcBlocksWithNonMovableCount()
        {
            throw new NotImplementedException();
        }

        private static bool MoveMakesSense(int destBlockIndex, ulong destOffset, int sourceBlockIndex,
                                           ulong sourceOffset)
        {
            throw new NotImplementedException();
        }
    }
}