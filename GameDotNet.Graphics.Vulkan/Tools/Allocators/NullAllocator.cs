using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan.Tools.Allocators;

public class NullAllocator : IVulkanAllocCallback
{
    public ref readonly AllocationCallbacks Handle => ref Unsafe.NullRef<AllocationCallbacks>();

    public unsafe void* Allocate(object? userData, nuint size, nuint alignment, SystemAllocationScope scope)
    {
        throw new NotImplementedException();
    }

    public unsafe void* Reallocate(object? userData, void* pOriginal, nuint size, nuint alignment,
        SystemAllocationScope scope)
    {
        throw new NotImplementedException();
    }

    public unsafe void Free(object? userData, void* pMemory)
    {
        throw new NotImplementedException();
    }

    public void InternalAllocNotification(object? userData, nuint size, InternalAllocationType type,
        SystemAllocationScope scope)
    {
        throw new NotImplementedException();
    }

    public void InternalFreeNotification(object? userData, nuint size, InternalAllocationType type,
        SystemAllocationScope scope)
    {
        throw new NotImplementedException();
    }

    //NoOp for null allocators, no need to clone
    public IVulkanAllocCallback WithUserData(object? userData = null) => this;
}