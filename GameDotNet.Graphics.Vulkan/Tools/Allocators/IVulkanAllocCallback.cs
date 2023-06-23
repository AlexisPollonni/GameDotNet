using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan.Tools.Allocators;

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