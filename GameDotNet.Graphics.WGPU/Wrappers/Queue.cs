using System.Runtime.InteropServices;
using Silk.NET.WebGPU;

using unsafe QueuePtr = Silk.NET.WebGPU.Queue*;

namespace GameDotNet.Graphics.WGPU.Wrappers;

public sealed class Queue : IDisposable
{
    private readonly WebGPU _api;
    private readonly unsafe QueuePtr _handle;

    internal unsafe Queue(WebGPU api, QueuePtr handle)
    {
        if (handle is null)
            throw new ResourceCreationError(nameof(Queue));

        _api = api;
        _handle = handle;
    }

    public unsafe void OnSubmittedWorkDone(QueueWorkDoneCallback callback)
    {
        _api.QueueOnSubmittedWorkDone(_handle,
                                      new((s, _) => callback(s)), 
                                      null
                                     );
    }

    public unsafe void Submit(params CommandBuffer[] commands)
    {
        Span<nint> commandBufferImpls = stackalloc nint[commands.Length];

        for (var i = 0; i < commands.Length; i++)
            commandBufferImpls[i] = (nint)commands[i].Handle;
            
        fixed(nint* ptr = commandBufferImpls)
            _api.QueueSubmit(_handle, (nuint)commands.Length, (Silk.NET.WebGPU.CommandBuffer**)ptr);
    }

    public unsafe void WriteBuffer<T>(Buffer buffer, ulong bufferOffset, ReadOnlySpan<T> data)
        where T : unmanaged
    {
        var structSize = (nuint)sizeof(T);


        _api.QueueWriteBuffer(_handle, buffer.Handle, bufferOffset, data, (nuint)data.Length * structSize);
    }

    public unsafe void WriteTexture<T>(ImageCopyTexture destination, ReadOnlySpan<T> data, 
                                       in TextureDataLayout dataLayout, in Extent3D writeSize)
        where T : unmanaged
    {
        var structSize = (nuint)Marshal.SizeOf<T>();


        _api.QueueWriteTexture(_handle, destination,
                          in data.GetPinnableReference(),
                          (nuint)data.Length * structSize,
                          dataLayout, in writeSize);
    }
        
    /// <summary>
    /// This function will be called automatically when this Queue's associated Device is disposed.
    /// </summary>
    public unsafe void Dispose()
    {
        _api.QueueRelease(_handle);
    }
}