using System.Diagnostics;
using System.Runtime.CompilerServices;
using GameDotNet.Core.Graphics.MemoryAllocation.Metadata;
using Silk.NET.Vulkan;

namespace GameDotNet.Core.Graphics.MemoryAllocation
{
    internal class BlockList : IDisposable
    {
        private const int AllocationTryCount = 32;

        private readonly List<VulkanMemoryBlock> _blocks = new();
        private readonly ReaderWriterLockSlim _mutex = new(LockRecursionPolicy.NoRecursion);

        private readonly int _minBlockCount, _maxBlockCount;
        private readonly bool _explicitBlockSize;

        private readonly Func<long, IBlockMetadata> _metaObjectCreate;

        private bool _hasEmptyBlock;
        private uint _nextBlockId;

        public BlockList(VulkanMemoryAllocator allocator, VulkanMemoryPool? pool, int memoryTypeIndex,
                         long preferredBlockSize, int minBlockCount, int maxBlockCount, long bufferImageGranularity,
                         int frameInUseCount, bool explicitBlockSize, Func<long, IBlockMetadata> algorithm)
        {
            Allocator = allocator;
            ParentPool = pool;
            MemoryTypeIndex = memoryTypeIndex;
            PreferredBlockSize = preferredBlockSize;
            _minBlockCount = minBlockCount;
            _maxBlockCount = maxBlockCount;
            BufferImageGranularity = bufferImageGranularity;
            FrameInUseCount = frameInUseCount;
            _explicitBlockSize = explicitBlockSize;

            _metaObjectCreate = algorithm;
        }

        public void Dispose()
        {
            foreach (var block in _blocks)
            {
                block.Dispose();
            }
        }

        public VulkanMemoryAllocator Allocator { get; }

        public VulkanMemoryPool? ParentPool { get; }

        public bool IsCustomPool => ParentPool != null;

        public int MemoryTypeIndex { get; }

        public long PreferredBlockSize { get; }

        public long BufferImageGranularity { get; }

        public int FrameInUseCount { get; }

        public bool IsEmpty
        {
            get
            {
                _mutex.EnterReadLock();

                try
                {
                    return _blocks.Count == 0;
                }
                finally
                {
                    _mutex.ExitReadLock();
                }
            }
        }

        public bool IsCorruptedDetectionEnabled => false;

        public int BlockCount => _blocks.Count;

        public VulkanMemoryBlock this[int index] => _blocks[index];

        private IEnumerable<VulkanMemoryBlock> BlocksInReverse //Just gonna take advantage of C#...
        {
            get
            {
                for (var index = _blocks.Count - 1; index >= 0; --index)
                {
                    yield return _blocks[index];
                }
            }
        }

        public void CreateMinBlocks()
        {
            if (_blocks.Count > 0)
            {
                throw new InvalidOperationException("Block list not empty");
            }

            for (var i = 0; i < _minBlockCount; ++i)
            {
                var res = CreateBlock(PreferredBlockSize, out _);

                if (res != Result.Success)
                {
                    throw new AllocationException("Unable to allocate device memory block", res);
                }
            }
        }

        public void GetPoolStats(out PoolStats stats)
        {
            _mutex.EnterReadLock();

            try
            {
                stats = new();
                stats.BlockCount = _blocks.Count;

                foreach (var block in _blocks)
                {
                    Debug.Assert(block != null);

                    block.Validate();

                    block.MetaData.AddPoolStats(ref stats);
                }
            }
            finally
            {
                _mutex.ExitReadLock();
            }
        }

        public Allocation Allocate(int currentFrame, long size, long alignment, in AllocationCreateInfo allocInfo,
                                   SuballocationType suballocType)
        {
            _mutex.EnterWriteLock();

            try
            {
                return AllocatePage(currentFrame, size, alignment, allocInfo, suballocType);
            }
            finally
            {
                _mutex.ExitWriteLock();
            }
        }

        public void Free(Allocation allocation)
        {
            VulkanMemoryBlock? blockToDelete = null;

            var budgetExceeded = false;
            {
                var heapIndex = Allocator.MemoryTypeIndexToHeapIndex(MemoryTypeIndex);
                Allocator.GetBudget(heapIndex, out var budget);
                budgetExceeded = budget.Usage >= budget.Budget;
            }

            _mutex.EnterWriteLock();

            try
            {
                var blockAlloc = (BlockAllocation)allocation;

                var block = blockAlloc.Block;

                //Corruption Detection TODO

                if (allocation.IsPersistantMapped)
                {
                    block.Unmap(1);
                }

                block.MetaData.Free(blockAlloc);

                block.Validate();

                var canDeleteBlock = _blocks.Count > _minBlockCount;

                if (block.MetaData.IsEmpty)
                {
                    if ((_hasEmptyBlock || budgetExceeded) && canDeleteBlock)
                    {
                        blockToDelete = block;
                        Remove(block);
                    }
                }
                else if (_hasEmptyBlock && canDeleteBlock)
                {
                    block = _blocks[^1];

                    if (block.MetaData.IsEmpty)
                    {
                        blockToDelete = block;
                        _blocks.RemoveAt(_blocks.Count - 1);
                    }
                }

                UpdateHasEmptyBlock();
                IncrementallySortBlocks();
            }
            finally
            {
                _mutex.ExitWriteLock();
            }

            if (blockToDelete != null)
            {
                blockToDelete.Dispose();
            }
        }

        public void AddStats(Stats stats)
        {
            var memTypeIndex = MemoryTypeIndex;
            var memHeapIndex = Allocator.MemoryTypeIndexToHeapIndex(memTypeIndex);

            _mutex.EnterReadLock();

            try
            {
                foreach (var block in _blocks)
                {
                    Debug.Assert(block != null);
                    block.Validate();

                    block.MetaData.CalcAllocationStatInfo(out var info);
                    StatInfo.Add(ref stats.Total, info);
                    StatInfo.Add(ref stats.MemoryType[memTypeIndex], info);
                    StatInfo.Add(ref stats.MemoryHeap[memHeapIndex], info);
                }
            }
            finally
            {
                _mutex.ExitReadLock();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="currentFrame"></param>
        /// <returns>
        /// Lost Allocation Count
        /// </returns>
        public int MakePoolAllocationsLost(int currentFrame)
        {
            _mutex.EnterWriteLock();

            try
            {
                var lostAllocationCount = 0;

                foreach (var block in _blocks)
                {
                    Debug.Assert(block != null);

                    lostAllocationCount += block.MetaData.MakeAllocationsLost(currentFrame, FrameInUseCount);
                }

                return lostAllocationCount;
            }
            finally
            {
                _mutex.ExitWriteLock();
            }
        }

        public Result CheckCorruption()
        {
            throw new NotImplementedException();
        }

        public int CalcAllocationCount()
        {
            return _blocks.Sum(block => block.MetaData.AllocationCount);
        }

        public bool IsBufferImageGranularityConflictPossible()
        {
            if (BufferImageGranularity == 1)
                return false;

            var lastSuballocType = SuballocationType.Free;

            foreach (var metadata in _blocks.Select(block => block.MetaData as BlockMetadata_Generic))
            {
                Debug.Assert(metadata != null);

                if (metadata.IsBufferImageGranularityConflictPossible(BufferImageGranularity,
                                                                      ref lastSuballocType))
                {
                    return true;
                }
            }

            return false;
        }

        private long CalcMaxBlockSize()
        {
            long result = 0;

            for (var i = _blocks.Count - 1; i >= 0; --i)
            {
                var blockSize = _blocks[i].MetaData.Size;

                if (result < blockSize)
                {
                    result = blockSize;
                }

                if (result >= PreferredBlockSize)
                {
                    break;
                }
            }

            return result;
        }

        [SkipLocalsInit]
        private Allocation AllocatePage(int currentFrame, long size, long alignment, in AllocationCreateInfo createInfo,
                                        SuballocationType suballocType)
        {
            var canMakeOtherLost = (createInfo.Flags & AllocationCreateFlags.CanMakeOtherLost) != 0;
            var mapped = (createInfo.Flags & AllocationCreateFlags.Mapped) != 0;

            long freeMemory;

            {
                var heapIndex = Allocator.MemoryTypeIndexToHeapIndex(MemoryTypeIndex);

                Allocator.GetBudget(heapIndex, out var heapBudget);

                freeMemory = (heapBudget.Usage < heapBudget.Budget) ? (heapBudget.Budget - heapBudget.Usage) : 0;
            }

            var canFallbackToDedicated = !IsCustomPool;
            var canCreateNewBlock = ((createInfo.Flags & AllocationCreateFlags.NeverAllocate) == 0) &&
                                    (_blocks.Count < _maxBlockCount) &&
                                    (freeMemory >= size || !canFallbackToDedicated);

            var strategy = createInfo.Strategy;

            //if (this.algorithm == (uint)PoolCreateFlags.LinearAlgorithm && this.maxBlockCount > 1)
            //{
            //    canMakeOtherLost = false;
            //}

            //if (isUpperAddress && (this.algorithm != (uint)PoolCreateFlags.LinearAlgorithm || this.maxBlockCount > 1))
            //{
            //    throw new AllocationException("Upper address allocation unavailable", Result.ErrorFeatureNotPresent);
            //}

            switch (strategy)
            {
                case 0:
                    strategy = AllocationStrategyFlags.BestFit;
                    break;
                case AllocationStrategyFlags.BestFit:
                case AllocationStrategyFlags.WorstFit:
                case AllocationStrategyFlags.FirstFit:
                    break;
                default:
                    throw new AllocationException("Invalid allocation strategy", Result.ErrorFeatureNotPresent);
            }

            if (size + 2 * Helpers.DebugMargin > PreferredBlockSize)
            {
                throw new AllocationException("Allocation size larger than block size", Result.ErrorOutOfDeviceMemory);
            }

            var context = new AllocationContext(
                                                currentFrame,
                                                FrameInUseCount,
                                                BufferImageGranularity,
                                                size,
                                                alignment,
                                                strategy,
                                                suballocType,
                                                canMakeOtherLost);

            Allocation? alloc;

            if (!canMakeOtherLost || canCreateNewBlock)
            {
                var allocFlagsCopy = createInfo.Flags & ~AllocationCreateFlags.CanMakeOtherLost;

                if (strategy == AllocationStrategyFlags.BestFit)
                {
                    foreach (var block in _blocks)
                    {
                        alloc = AllocateFromBlock(block, in context, allocFlagsCopy, createInfo.UserData);

                        if (alloc != null)
                        {
                            //Possibly Log here
                            return alloc;
                        }
                    }
                }
                else
                {
                    foreach (var curBlock in BlocksInReverse)
                    {
                        alloc = AllocateFromBlock(curBlock, in context, allocFlagsCopy, createInfo.UserData);

                        if (alloc != null)
                        {
                            //Possibly Log here
                            return alloc;
                        }
                    }
                }
            }

            if (canCreateNewBlock)
            {
                var allocFlagsCopy = createInfo.Flags & ~AllocationCreateFlags.CanMakeOtherLost;

                var newBlockSize = PreferredBlockSize;
                var newBlockSizeShift = 0;
                const int newBlockSizeShiftMax = 3;

                if (!_explicitBlockSize)
                {
                    var maxExistingBlockSize = CalcMaxBlockSize();

                    for (var i = 0; i < newBlockSizeShiftMax; ++i)
                    {
                        var smallerNewBlockSize = newBlockSize / 2;
                        if (smallerNewBlockSize > maxExistingBlockSize && smallerNewBlockSize >= size * 2)
                        {
                            newBlockSize = smallerNewBlockSize;
                            newBlockSizeShift += 1;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                var newBlockIndex = 0;

                var res = (newBlockSize <= freeMemory || !canFallbackToDedicated)
                              ? CreateBlock(newBlockSize, out newBlockIndex)
                              : Result.ErrorOutOfDeviceMemory;

                if (!_explicitBlockSize)
                {
                    while (res < 0 && newBlockSizeShift < newBlockSizeShiftMax)
                    {
                        var smallerNewBlockSize = newBlockSize / 2;

                        if (smallerNewBlockSize >= size)
                        {
                            newBlockSize = smallerNewBlockSize;
                            newBlockSizeShift += 1;
                            res = (newBlockSize <= freeMemory || !canFallbackToDedicated)
                                      ? CreateBlock(newBlockSize, out newBlockIndex)
                                      : Result.ErrorOutOfDeviceMemory;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                if (res == Result.Success)
                {
                    var block = _blocks[newBlockIndex];

                    alloc = AllocateFromBlock(block, in context, allocFlagsCopy, createInfo.UserData);

                    if (alloc != null)
                    {
                        //Possibly Log here
                        return alloc;
                    }
                }
            }

            if (!canMakeOtherLost) throw new AllocationException("Unable to allocate memory");
            var tryIndex = 0;

            for (; tryIndex < AllocationTryCount; ++tryIndex)
            {
                VulkanMemoryBlock? bestRequestBlock = null;

                Unsafe.SkipInit(out AllocationRequest bestAllocRequest);

                var bestRequestCost = long.MaxValue;

                if (strategy == AllocationStrategyFlags.BestFit)
                {
                    foreach (var curBlock in _blocks)
                    {
                        if (!curBlock.MetaData.TryCreateAllocationRequest(in context, out var request))
                            continue;
                        var currRequestCost = request.CalcCost();

                        if (bestRequestBlock != null && currRequestCost >= bestRequestCost)
                            continue;
                        bestRequestBlock = curBlock;
                        bestAllocRequest = request;
                        bestRequestCost = currRequestCost;

                        if (bestRequestCost == 0)
                            break;
                    }
                }
                else
                {
                    foreach (var curBlock in BlocksInReverse)
                    {
                        if (!curBlock.MetaData.TryCreateAllocationRequest(in context, out var request))
                            continue;
                        var curRequestCost = request.CalcCost();

                        if (bestRequestBlock != null && curRequestCost >= bestRequestCost &&
                            strategy != AllocationStrategyFlags.FirstFit)
                            continue;
                        bestRequestBlock = curBlock;
                        bestRequestCost = curRequestCost;
                        bestAllocRequest = request;

                        if (bestRequestCost == 0 || strategy == AllocationStrategyFlags.FirstFit)
                        {
                            break;
                        }
                    }
                }

                if (bestRequestBlock != null)
                {
                    if (mapped)
                    {
                        bestRequestBlock.Map(1);
                    }

                    if (!bestRequestBlock.MetaData.MakeRequestedAllocationsLost(currentFrame, FrameInUseCount,
                                                                                    ref bestAllocRequest))
                        continue;
                    var talloc = new BlockAllocation(Allocator, Allocator.CurrentFrameIndex);

                    bestRequestBlock.MetaData.Alloc(in bestAllocRequest, suballocType, size, talloc);

                    UpdateHasEmptyBlock();

                    //(allocation as BlockAllocation).InitBlockAllocation();

                    try
                    {
                        bestRequestBlock.Validate(); //Won't be called in release builds
                    }
                    catch
                    {
                        talloc.Dispose();
                        throw;
                    }

                    talloc.UserData = createInfo.UserData;

                    Allocator.Budget
                             .AddAllocation(Allocator.MemoryTypeIndexToHeapIndex(MemoryTypeIndex), size);

                    //Maybe put memory init and corruption detection here

                    return talloc;
                }
                else
                {
                    break;
                }
            }

            if (tryIndex == AllocationTryCount)
            {
                throw new AllocationException("", Result.ErrorTooManyObjects);
            }

            throw new AllocationException("Unable to allocate memory");
        }

        private Allocation? AllocateFromBlock(VulkanMemoryBlock block, in AllocationContext context,
                                              AllocationCreateFlags flags, object? userData)
        {
            Debug.Assert((flags & AllocationCreateFlags.CanMakeOtherLost) == 0);
            var mapped = (flags & AllocationCreateFlags.Mapped) != 0;

            if (!block.MetaData.TryCreateAllocationRequest(in context, out var request))
                return null;
            Debug.Assert(request.ItemsToMakeLostCount == 0);

            if (mapped)
            {
                block.Map(1);
            }

            var allocation = new BlockAllocation(Allocator, Allocator.CurrentFrameIndex);

            block.MetaData.Alloc(in request, context.SuballocationType, context.AllocationSize, allocation);

            allocation.InitBlockAllocation(block, request.Offset, context.AllocationAlignment,
                                           context.AllocationSize, MemoryTypeIndex,
                                           context.SuballocationType, mapped,
                                           (flags & AllocationCreateFlags.CanBecomeLost) != 0);

            UpdateHasEmptyBlock();

            block.Validate();

            allocation.UserData = userData;

            Allocator.Budget.AddAllocation(Allocator.MemoryTypeIndexToHeapIndex(MemoryTypeIndex),
                                           context.AllocationSize);

            return allocation;
        }

        private unsafe Result CreateBlock(long blockSize, out int newBlockIndex)
        {
            newBlockIndex = -1;

            var info = new MemoryAllocateInfo
            {
                SType = StructureType.MemoryAllocateInfo,
                MemoryTypeIndex = (uint)MemoryTypeIndex,
                AllocationSize = (ulong)blockSize
            };

            // Every standalone block can potentially contain a buffer with BufferUsageFlags.BufferUsageShaderDeviceAddressBitKhr - always enable the feature
            var allocFlagsInfo =
                new MemoryAllocateFlagsInfoKHR(StructureType.MemoryAllocateFlagsInfoKhr);
            if (Allocator.UseKhrBufferDeviceAddress)
            {
                allocFlagsInfo.Flags = MemoryAllocateFlags.AddressBitKhr;
                info.PNext = &allocFlagsInfo;
            }

            var res = Allocator.AllocateVulkanMemory(in info, out var mem);

            if (res < 0)
            {
                return res;
            }

            var metaObject = _metaObjectCreate(blockSize);

            if (metaObject.Size != blockSize)
            {
                throw new InvalidOperationException("Returned Metadata object reports incorrect block size");
            }

            var block = new VulkanMemoryBlock(Allocator, ParentPool, MemoryTypeIndex, mem,
                                              _nextBlockId++, metaObject);

            _blocks.Add(block);

            newBlockIndex = _blocks.Count - 1;

            return Result.Success;
        }

        private void FreeEmptyBlocks(ref Defragmentation.DefragmentationStats stats)
        {
            for (var i = _blocks.Count - 1; i >= 0; --i)
            {
                var block = _blocks[i];

                if (!block.MetaData.IsEmpty)
                    continue;
                if (_blocks.Count > _minBlockCount)
                {
                    stats.DeviceMemoryBlocksFreed += 1;
                    stats.BytesFreed += block.MetaData.Size;

                    _blocks.RemoveAt(i);
                    block.Dispose();
                }
                else
                {
                    break;
                }
            }

            UpdateHasEmptyBlock();
        }

        private void UpdateHasEmptyBlock()
        {
            _hasEmptyBlock = _blocks.Any(block => block.MetaData.IsEmpty);
        }

        private void Remove(VulkanMemoryBlock block)
        {
            var res = _blocks.Remove(block);
            Debug.Assert(res, "");
        }

        private void IncrementallySortBlocks()
        {
            if ((uint)_blocks.Count <= 1)
                return;
            var prevBlock = _blocks[0];
            var i = 1;

            do
            {
                var curBlock = _blocks[i];

                if (prevBlock.MetaData.SumFreeSize > curBlock.MetaData.SumFreeSize)
                {
                    _blocks[i - 1] = curBlock;
                    _blocks[i] = prevBlock;
                    return;
                }

                prevBlock = curBlock;
                i += 1;
            } while (i < _blocks.Count);
        }

        public class DefragmentationContext
        {
            private readonly BlockList _list;

            public DefragmentationContext(BlockList list)
            {
                _list = list;
            }

            //public void Defragment(DefragmentationStats stats, DefragmentationFlags flags, ulong maxCpuBytesToMove, )

            //public void End(DefragmentationStats stats)

            //public uint ProcessDefragmentations(DefragmentationPassMoveInfo move, uint maxMoves)

            //public void CommitDefragmentations(DefragmentationStats stats)
        }
    }
}