using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan.Tools.Allocators;

public class TrackedMemoryAllocator : BaseVulkanAllocator
{
    private readonly ConcurrentDictionary<nint, AllocDesc> _currentAllocations;
    
    private long _totalAllocSize;
    private long _internalAllocSize;


    public TrackedMemoryAllocator(object? userData) : base(userData)
    {
        _currentAllocations = new();
    }

    public override unsafe void* Allocate(object? userData, nuint size, nuint alignment, SystemAllocationScope scope)
    {
        if (size is 0) return null;
        
        var mem = NativeMemory.AlignedAlloc(size, alignment);
        
        AddAllocation(new()
        {                                 
            Address = (nint) mem,
            Size = size,
            Scope = scope,
            UserData = userData
        });
        
        return mem;
    }

    public override unsafe void* Reallocate(object? userData, void* pOriginal, nuint size, nuint alignment, SystemAllocationScope scope)
    {
        if (size is 0)
        {
            //If size is 0 we free the original memory and return
            Free(userData, pOriginal);
            return null;
        }

        var mem = NativeMemory.AlignedRealloc(pOriginal, size, alignment);
        
        RemoveAllocation(pOriginal);
        AddAllocation(new()
        {
            Address = (nint) mem,
            Size = size,
            Scope = scope,
            UserData = userData
        });

        return mem;
    }

    public override unsafe void Free(object? userData, void* pMemory)
    {
        if(pMemory is null) return;
        
        NativeMemory.AlignedFree(pMemory);
        RemoveAllocation(pMemory);
    }

    public override void InternalAllocNotification(object? userData, nuint size, InternalAllocationType type, SystemAllocationScope scope)
    {
        GC.AddMemoryPressure((long) size);
        Interlocked.Add(ref _internalAllocSize, (long)size);
    }

    public override void InternalFreeNotification(object? userData, nuint size, InternalAllocationType type, SystemAllocationScope scope)
    {
        GC.RemoveMemoryPressure((long) size);
        Interlocked.Add(ref _internalAllocSize, -(long) size);
    }

    private void AddAllocation(in AllocDesc desc)
    {
        _currentAllocations[desc.Address] = desc;
        GC.AddMemoryPressure((long) desc.Size);
        Interlocked.Add(ref _totalAllocSize, (long) desc.Size);
    }

    private unsafe void RemoveAllocation(void* ptr)
    {
        if(_currentAllocations.TryRemove((nint) ptr, out var desc))
        {
            GC.RemoveMemoryPressure((long) desc.Size);
            Interlocked.Add(ref _totalAllocSize, -(long) desc.Size);
        }
    }
    
    private struct AllocDesc
    {
        public object? UserData { get; init; }
        public required nint Address { get; init; }
        public required nuint Size { get; init; }
        public required SystemAllocationScope Scope { get; init; }
    }
}