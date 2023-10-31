using System.Runtime.InteropServices;
using Silk.NET.WebGPU;
using unsafe BufferPtr = Silk.NET.WebGPU.Buffer*;

namespace GameDotNet.Graphics.WGPU.Wrappers;

public sealed class Buffer : IDisposable
{
    private readonly WebGPU _api;
    private unsafe BufferPtr _handle;

    internal unsafe BufferPtr Handle 
    {
        get
        {
            if (_handle is null)
                throw new HandleDroppedOrDestroyedException(nameof(Buffer));

            return _handle;
        }

        private set => _handle = value;
    }

    public ulong SizeInBytes { get; private set; }

    internal unsafe Buffer(WebGPU api, BufferPtr handle, in BufferDescriptor descriptor)
    {
        if (handle is null)
            throw new ResourceCreationError(nameof(Buffer));
        _api = api;

        Handle = handle;

        SizeInBytes = descriptor.Size;
    }

    public unsafe Span<T> GetConstMappedRange<T>(ulong offset, int size)
        where T : unmanaged
    {
        var structSize = (nuint)Marshal.SizeOf<T>();

        var ptr = _api.BufferGetConstMappedRange(_handle, (nuint)(offset * structSize), (nuint)size * structSize);

        return new(ptr, size);
    }

    public unsafe Span<T> GetMappedRange<T>(ulong offset, int size)
        where T : unmanaged
    {
        var structSize = (nuint)Marshal.SizeOf<T>();

        var ptr = _api.BufferGetMappedRange(_handle,
                                            (nuint)(offset * structSize), (nuint)size * structSize);

        return new(ptr, size);
    }

    public unsafe void MapAsync(MapMode mode, nuint offset, nuint size, BufferMapCallback callback)
    {
        _api.BufferMapAsync(_handle, mode, offset, size, new((s, _) => callback(s)), null);
    }

    public unsafe void Unmap() => _api.BufferUnmap(_handle);

    public unsafe void Dispose()
    {
        _api.BufferDestroy(_handle);
        _api.BufferRelease(_handle);
        _handle = null;
    }
}

public static class BufferExtensions
{
    public static void SetData<T>(this Buffer buffer, ulong offset, ReadOnlySpan<T> span)
        where T : unmanaged
    {
        var dest = buffer.GetMappedRange<T>(offset, span.Length);

        span.CopyTo(dest);
    }
}