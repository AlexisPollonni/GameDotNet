﻿using System.Diagnostics;
using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan.MemoryAllocation
{
    public sealed class BlockAllocation : Allocation
    {
        internal VulkanMemoryBlock Block;
        internal long offset;
        internal SuballocationType suballocationType;
        internal bool canBecomeLost;

        internal BlockAllocation(VulkanMemoryAllocator allocator, int currentFrameIndex) :
            base(allocator, currentFrameIndex)
        { }

        public override DeviceMemory DeviceMemory => Block.DeviceMemory;

        public override long Offset
        {
            get => offset;
            internal set => offset = value;
        }

        public override IntPtr MappedData
        {
            get
            {
                if (mapCount == 0) return default;
                var mapdata = Block.MappedData;

                Debug.Assert(mapdata != default);

                return new(mapdata.ToInt64() + offset);
            }
        }

        internal override bool CanBecomeLost => canBecomeLost;

        internal void InitBlockAllocation(VulkanMemoryBlock block, long offset, long alignment, long size,
                                          int memoryTypeIndex, SuballocationType subType, bool mapped,
                                          bool canBecomeLost)
        {
            Block = block;
            this.offset = offset;
            this.alignment = alignment;
            this.size = size;
            this.memoryTypeIndex = memoryTypeIndex;
            mapCount = mapped ? int.MinValue : 0;
            suballocationType = subType;
            this.canBecomeLost = canBecomeLost;
        }

        internal void ChangeAllocation(VulkanMemoryBlock block, long offset)
        {
            Debug.Assert(block is not null && offset >= 0);

            if (!ReferenceEquals(block, Block))
            {
                var mapRefCount = mapCount & int.MaxValue;

                if (IsPersistantMapped)
                {
                    mapRefCount += 1;
                }

                Block.Unmap(mapRefCount);
                block.Map(mapRefCount);

                Block = block;
            }

            Offset = offset;
        }

        private void BlockAllocMap()
        {
            if ((mapCount & int.MaxValue) < int.MaxValue)
            {
                mapCount += 1;
            }
            else
            {
                throw new InvalidOperationException("Allocation mapped too many times simultaneously");
            }
        }

        private void BlockAllocUnmap()
        {
            if ((mapCount & int.MaxValue) > 0)
            {
                mapCount -= 1;
            }
            else
            {
                throw new InvalidOperationException("Unmapping allocation not previously mapped");
            }
        }

        public override IntPtr Map()
        {
            if (CanBecomeLost)
            {
                throw new InvalidOperationException("Cannot map an allocation that can become lost");
            }

            var data = Block.Map(1);

            data = new(data.ToInt64() + Offset);

            BlockAllocMap();

            return data;
        }

        public override void Unmap()
        {
            BlockAllocUnmap();
            Block.Unmap(1);
        }
    }
}