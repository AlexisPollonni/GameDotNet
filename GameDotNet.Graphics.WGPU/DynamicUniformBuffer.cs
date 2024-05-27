using System.Runtime.InteropServices;
using ByteSizeLib;
using Collections.Pooled;
using CommunityToolkit.HighPerformance;
using GameDotNet.Core.Tools.Extensions;
using Silk.NET.WebGPU;
using Buffer = GameDotNet.Graphics.WGPU.Wrappers.Buffer;
using Device = GameDotNet.Graphics.WGPU.Wrappers.Device;

namespace GameDotNet.Graphics.WGPU;

public sealed class DynamicUniformBuffer<T> : IDisposable
    where T : unmanaged
{
    private readonly Device _device;
    public Buffer? GpuBuffer { get; private set; }

    public ByteSize Size => ByteSize.FromBytes(GpuBuffer?.Size.GetBytes() ?? 0);
    public ByteSize Stride { get; }
    public IList<T> Items => _items;

    private readonly uint _itemSize;
    private readonly List<T> _items;

    public DynamicUniformBuffer(Device device)
    {
        _device = device;
        _items = [];
        _itemSize = (uint)Marshal.SizeOf<T>();
        
        device.GetLimits(out var supported);

        var bStride = CeilToNextMultiple((uint)Marshal.SizeOf<T>(), supported.Limits.MinUniformBufferOffsetAlignment);
        Stride = ByteSize.FromBytes(bStride);
    }

    public DynamicUniformBuffer(Device device, IEnumerable<T> items) : this(device)
    {
        _items.AddRange(items);
    }

    public void SubmitWrite()
    {
        if (_items.Count == 0)
        {
            GpuBuffer?.Dispose();
            GpuBuffer = null;
            return;
        }
        
        var totalSize = _items.Count * Stride.GetBytes();
        if(GpuBuffer is null || (ulong)totalSize != GpuBuffer.Size.GetBytes()) //Maybe use > here?
        {
            GpuBuffer?.Dispose();
            GpuBuffer = _device.CreateBuffer("dyn-uniform", true, (ulong)totalSize, BufferUsage.Uniform | BufferUsage.CopyDst);
            
            var map = GpuBuffer.GetMappedRange<byte>(0, (nuint)GpuBuffer.Size.GetBytes());

            WriteTo(map);
        
            GpuBuffer.Unmap();
        }
        else
        {
            using var data = new PooledList<byte>((int)totalSize);
            
            WriteTo(data.Span);
            
            _device.Queue.WriteBuffer<byte>(GpuBuffer, data.Span);
        }

        void WriteTo(Span<byte> dst)
        {
            for (var i = 0; i < _items.Count; i++)
            {
                var pos = i * Stride.GetBytes();

                var slice = dst.Slice((int)pos, (int)_itemSize).Cast<byte, T>();

                slice[0] = _items[i];
            }
        }
    }

    public void Dispose()
    {
        GpuBuffer?.Dispose();
    }

    private static uint CeilToNextMultiple(uint value, uint step)
    {
        var divideCeil = value / step + (value % step == 0 ? 0 : 1);
        return (uint)(step * divideCeil);
    }
}