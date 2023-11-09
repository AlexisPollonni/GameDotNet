using Silk.NET.SPIRV.Reflect;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.Dawn;

namespace GameDotNet.Graphics.WGPU.Extensions;

public static class UtilsExtensions
{
    public static unsafe Dawn? GetDawnExtension(this WebGPU api)
    {
        api.TryGetDeviceExtension(null, out Dawn? dawn);

        return dawn;
    }
    
    public static VertexFormat ToVertexFormat(this Format format)
        => format switch
        {
            Format.Undefined => VertexFormat.Undefined,
            Format.R32Uint => VertexFormat.Uint32,
            Format.R32Sint => VertexFormat.Sint32,
            Format.R32Sfloat => VertexFormat.Float32,
            Format.R32G32Uint => VertexFormat.Uint32x2,
            Format.R32G32Sint => VertexFormat.Sint32x2,
            Format.R32G32Sfloat => VertexFormat.Float32x2,
            Format.R32G32B32Uint => VertexFormat.Uint32x3,
            Format.R32G32B32Sint => VertexFormat.Sint32x3,
            Format.R32G32B32Sfloat => VertexFormat.Float32x3,
            Format.R32G32B32A32Uint => VertexFormat.Uint32x4,
            Format.R32G32B32A32Sint => VertexFormat.Sint32x4,
            Format.R32G32B32A32Sfloat => VertexFormat.Float32x4,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };

    public static ulong GetByteSize(this VertexFormat format)
        => format switch
        {
            VertexFormat.Undefined => 0,
            VertexFormat.Uint8x2 => 2,
            VertexFormat.Uint8x4 => 4,
            VertexFormat.Sint8x2 => 2,
            VertexFormat.Sint8x4 => 4,
            VertexFormat.Unorm8x2 => 2,
            VertexFormat.Unorm8x4 => 4,
            VertexFormat.Snorm8x2 => 2,
            VertexFormat.Snorm8x4 => 4,
            VertexFormat.Uint16x2 => 4,
            VertexFormat.Uint16x4 => 8,
            VertexFormat.Sint16x2 => 4,
            VertexFormat.Sint16x4 => 8,
            VertexFormat.Unorm16x2 => 4,
            VertexFormat.Unorm16x4 => 8,
            VertexFormat.Snorm16x2 => 4,
            VertexFormat.Snorm16x4 => 8,
            VertexFormat.Float16x2 => 4,
            VertexFormat.Float16x4 => 8,
            VertexFormat.Float32 => 4,
            VertexFormat.Float32x2 => 8,
            VertexFormat.Float32x3 => 12,
            VertexFormat.Float32x4 => 16,
            VertexFormat.Uint32 => 4,
            VertexFormat.Uint32x2 => 8,
            VertexFormat.Uint32x3 => 12,
            VertexFormat.Uint32x4 => 16,
            VertexFormat.Sint32 => 4,
            VertexFormat.Sint32x2 => 8,
            VertexFormat.Sint32x3 => 12,
            VertexFormat.Sint32x4 => 16,
            VertexFormat.Force32 => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };
}