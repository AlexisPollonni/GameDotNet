using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Serilog;
using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan.Tools;

public interface IVulkanAllocCallback
{
    public ref readonly AllocationCallbacks Handle { get; }

    protected unsafe void* Allocate(object? userData, nuint size, nuint alignment, SystemAllocationScope scope);

    protected unsafe void* Reallocate(object? userData, void* pOriginal, nuint size, nuint alignment,
                                      SystemAllocationScope scope);

    protected unsafe void Free(object? userData, void* pMemory);

    protected void InternalAllocNotification(object? userData, nuint size, InternalAllocationType type,
                                             SystemAllocationScope scope);

    protected void InternalFreeNotification(object? userData, nuint size, InternalAllocationType type,
                                            SystemAllocationScope scope);

    public IVulkanAllocCallback WithUserData(object? userData = null);
}

public abstract class BaseVulkanAllocator : IVulkanAllocCallback
{
    public ref readonly AllocationCallbacks Handle => ref _callbacks;


    private AllocationCallbacks _callbacks;

    protected BaseVulkanAllocator(object? userData)
    {
        _callbacks = ToCallbacks(userData);
    }

    public IVulkanAllocCallback WithUserData(object? userData = null)
    {
        var other = (BaseVulkanAllocator)MemberwiseClone();
        other._callbacks = ToCallbacks(userData);

        return other;
    }


    public abstract unsafe void* Allocate(object? userData, nuint size, nuint alignment, SystemAllocationScope scope);

    public abstract unsafe void* Reallocate(object? userData, void* pOriginal, nuint size, nuint alignment,
                                            SystemAllocationScope scope);

    public abstract unsafe void Free(object? userData, void* pMemory);

    public abstract void InternalAllocNotification(object? userData, nuint size, InternalAllocationType type,
                                                   SystemAllocationScope scope);

    public abstract void InternalFreeNotification(object? userData, nuint size, InternalAllocationType type,
                                                  SystemAllocationScope scope);

    private unsafe AllocationCallbacks ToCallbacks(object? userData)
    {
        return new(null,
                   new((_, size, alignment, scope) => Allocate(userData, size, alignment, scope)),
                   new((_, ptr, size, alignment, scope) => Reallocate(userData, ptr, size, alignment, scope)),
                   new((_, ptr) => Free(userData, ptr)),
                   new((_, size, type, scope) => InternalAllocNotification(userData, size, type, scope)),
                   new((_, size, type, scope) => InternalFreeNotification(userData, size, type, scope))
                  );
    }
}

public class DebugLoggingVulkanAllocator : BaseVulkanAllocator
{
    private readonly ILogger _logger;

    public DebugLoggingVulkanAllocator(ILogger logger, string area) : base(area)
    {
        _logger = logger;
    }

    public override unsafe void* Allocate(object? userData, nuint size, nuint alignment, SystemAllocationScope scope)
    {
        var mem = NativeMemory.AlignedAlloc(size, alignment);

        _logger.Verbose("<Vulkan | Memory> (Object: {Data}) New allocation at {Ptr} {{Size={Size}, Align={Align}, Scope={Scope}}}",
                        userData, (nuint)mem, size, alignment, scope);

        return mem;
    }

    public override unsafe void* Reallocate(object? userData, void* pOriginal, nuint size, nuint alignment,
                                            SystemAllocationScope scope)
    {
        void* realloc;

        if (size is 0)
        {
            //If size is 0 we free the original memory and return
            NativeMemory.AlignedFree(pOriginal);

            _logger.Verbose("<Vulkan | Memory> (Object: {Data}) Realloc freed at {Ptr} {{Align={Align}, Scope={Scope}}}",
                            userData, (nint)pOriginal, alignment, scope);
            return null;
        }


        realloc = NativeMemory.AlignedRealloc(pOriginal, size, alignment);
        _logger.Verbose("<Vulkan | Memory> (Object: {Data}) Reallocated {Ptr1} at {Ptr2} {{Size={Size}, Align={Align}, Scope={Scope}}}",
                        userData, (nuint)pOriginal, (nuint)realloc, size, alignment, scope);

        return realloc;
    }

    public override unsafe void Free(object? userData, void* pMemory)
    {
        if (pMemory is null) return;

        _logger.Verbose("<Vulkan | Memory> (Object: {Data}) Freed memory at {Ptr}",
                        userData, (nint)pMemory);
        NativeMemory.AlignedFree(pMemory);
    }

    public override void InternalAllocNotification(object? userData, nuint size, InternalAllocationType type,
                                                   SystemAllocationScope scope)
    {
        _logger.Verbose("<Vulkan | Memory> (Object: {Data}) Internal allocation {{Size={Size}, Type={Type}, Scope={Scope}}}",
                        userData, size, type, scope);
    }

    public override void InternalFreeNotification(object? userData, nuint size, InternalAllocationType type,
                                                  SystemAllocationScope scope)
    {
        _logger.Verbose("<Vulkan | Memory> (Object: {Data}) Freed internal {{Size={Size}, Type={Type}, Scope={Scope}}}",
                        userData, size, type, scope);
    }
}

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