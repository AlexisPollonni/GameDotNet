using GameDotNet.Graphics.Vulkan.MemoryAllocation.Metadata;
using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan.MemoryAllocation.Defragmentation
{
    internal sealed class FastDefragAlgorithm : DefragmentationAlgorithm
    {
        private readonly bool _overlappingMoveSupported;
        private int _allocationCount;
        private bool _allAllocations;

        private ulong _bytesMoved;
        private int _allocationsMoved;

        private readonly List<BlockInfo> _blockInfos = new();

        public FastDefragAlgorithm(VulkanMemoryAllocator allocator, BlockList list, uint currentFrame,
                                   bool overlappingMoveSupported) : base(allocator, list, currentFrame)
        {
            this._overlappingMoveSupported = overlappingMoveSupported;
        }

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

        private void PreprocessMetadata()
        { }

        private void PostprocessMetadata()
        { }

        private void InsertSuballoc(BlockMetadata_Generic metadata, in Suballocation suballoc)
        { }

        private struct BlockInfo
        {
            public int OrigBlockIndex;
        }

        private class FreeSpaceDatabase
        {
            private const int MaxCount = 4;

            private FreeSpace[] _freeSpaces = new FreeSpace[MaxCount];

            public FreeSpaceDatabase()
            {
                for (var i = 0; i < _freeSpaces.Length; ++i)
                {
                    _freeSpaces[i].BlockInfoIndex = -1;
                }
            }

            public void Register(int blockInfoIndex, long offset, long size)
            {
                if (size < Helpers.MinFreeSuballocationSizeToRegister)
                {
                    return;
                }

                var bestIndex = -1;
                for (var i = 0; i < _freeSpaces.Length; ++i)
                {
                    ref var space = ref _freeSpaces[i];

                    if (space.BlockInfoIndex == -1)
                    {
                        bestIndex = i;
                        break;
                    }

                    if (space.Size < size && (bestIndex == -1 || space.Size < _freeSpaces[bestIndex].Size))
                    {
                        bestIndex = i;
                    }
                }

                if (bestIndex == -1) return;
                ref var bestSpace = ref _freeSpaces[bestIndex];

                bestSpace.BlockInfoIndex = blockInfoIndex;
                bestSpace.Offset = offset;
                bestSpace.Size = size;
            }

            public bool Fetch(long alignment, long size, out int blockInfoIndex, out long destOffset)
            {
                var bestIndex = -1;
                long bestFreeSpaceAfter = 0;

                for (var i = 0; i < _freeSpaces.Length; ++i)
                {
                    ref var space = ref _freeSpaces[i];

                    if (space.BlockInfoIndex == -1)
                        break;

                    var tmpOffset = Helpers.AlignUp(space.Offset, alignment);

                    if (tmpOffset + size > space.Offset + space.Size) continue;

                    var freeSpaceAfter = space.Offset + space.Size - (tmpOffset + size);

                    if (bestIndex != -1 && freeSpaceAfter <= bestFreeSpaceAfter) continue;

                    bestIndex = i;
                    bestFreeSpaceAfter = freeSpaceAfter;
                }

                if (bestIndex != -1)
                {
                    ref var bestSpace = ref _freeSpaces[bestIndex];

                    blockInfoIndex = bestSpace.BlockInfoIndex;
                    destOffset = Helpers.AlignUp(bestSpace.Offset, alignment);

                    if (bestFreeSpaceAfter >= Helpers.MinFreeSuballocationSizeToRegister)
                    {
                        var alignmentPlusSize = (destOffset - bestSpace.Offset) + size;

                        bestSpace.Offset += alignmentPlusSize;
                        bestSpace.Size -= alignmentPlusSize;
                    }
                    else
                    {
                        bestSpace.BlockInfoIndex = -1;
                    }

                    return true;
                }

                blockInfoIndex = default;
                destOffset = default;
                return false;
            }

            private struct FreeSpace
            {
                public int BlockInfoIndex;
                public long Offset, Size;
            }
        }
    }
}