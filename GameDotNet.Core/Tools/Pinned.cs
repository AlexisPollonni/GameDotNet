using System.Runtime.InteropServices;

namespace GameDotNet.Core.Tools;

public class Pinned<T> : IDisposable where T : unmanaged
{
    private GCHandle _handle;

    public Pinned(in T source)
    {
        _handle = GCHandle.Alloc(source, GCHandleType.Pinned);
    }

    public unsafe T* AsPtr()
    {
        return (T*)_handle.AddrOfPinnedObject();
    }

    public void Dispose()
    {
        _handle.Free();
        GC.SuppressFinalize(this);
    }
}