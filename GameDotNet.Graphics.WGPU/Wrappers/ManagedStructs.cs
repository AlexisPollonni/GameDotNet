using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.Dawn;

namespace GameDotNet.Graphics.WGPU.Wrappers;

public delegate void RequestAdapterCallback(RequestAdapterStatus status, Adapter adapter, string message);

public delegate void RequestDeviceCallback(RequestDeviceStatus status, Device device, string message);

public delegate void ErrorCallback(ErrorType type, string message);

public delegate void LoggingCallback(LoggingType type, string message);

public delegate void DeviceLostCallback(DeviceLostReason reason, string message);

public delegate void BufferMapCallback(BufferMapAsyncStatus status);

public delegate void CompilationInfoCallback(CompilationInfoRequestStatus status,
                                             ReadOnlySpan<CompilationMessage> messages);

public delegate void QueueWorkDoneCallback(QueueWorkDoneStatus status);

public unsafe struct DawnInstanceDescriptor
{
    public InstanceFeatures Features;
    public ChainedStruct* Next;
}

public struct RequiredLimits
{
    public Limits Limits;
}

public partial struct RequiredLimitsExtras
{
    public uint MaxPushConstantSize;
}

public struct DeviceExtras
{
    public string TracePath;
}


public struct RenderPassColorAttachment
{
    public TextureView? View;

    public TextureView? ResolveTarget;

    public LoadOp LoadOp;

    public StoreOp StoreOp;

    public Color ClearValue;
}

/// <summary>
/// The dawn implementation RenderPassColorAttachment has a different memory layout than the Silk.Net bindings.
/// Fixes segfaults while this is fixed
/// </summary>
internal unsafe struct DawnRenderPassColorAttachment
{
    public ChainedStruct* NextInChain = null;
    public Silk.NET.WebGPU.TextureView* View = default;
    public uint DepthSlice = 0;
    public Silk.NET.WebGPU.TextureView* ResolveTarget = default;
    public LoadOp LoadOp = LoadOp.Undefined;
    public StoreOp StoreOp = StoreOp.Undefined;
    public Color ClearValue = default;

    public DawnRenderPassColorAttachment()
    { }
}

public struct RenderPassDepthStencilAttachment
{
    public TextureView View;

    public LoadOp DepthLoadOp;

    public StoreOp DepthStoreOp;

    public float DepthClearValue;

    public bool DepthReadOnly;

    public LoadOp StencilLoadOp;

    public StoreOp StencilStoreOp;

    public uint StencilClearValue;

    public bool StencilReadOnly;
}

public struct ImageCopyTexture
{
    public Texture Texture;

    public uint MipLevel;

    public Origin3D Origin;

    public TextureAspect Aspect;

    public static implicit operator Silk.NET.WebGPU.ImageCopyTexture(ImageCopyTexture t)
    {
        unsafe
        {
            return new()
            {
                Texture = t.Texture.Handle,
                MipLevel = t.MipLevel,
                Origin = t.Origin,
                Aspect = t.Aspect
            };
        }
    }
}

public struct ImageCopyBuffer
{
    public Buffer Buffer;

    public TextureDataLayout TextureDataLayout;

    public static implicit operator Silk.NET.WebGPU.ImageCopyBuffer(ImageCopyBuffer t)
    {
        unsafe
        {
            return new()
            {
                Layout = t.TextureDataLayout,
                Buffer = t.Buffer.Handle
            };
        }
    }
}



public struct BindGroupEntry
{
    public uint Binding;
    public Buffer? Buffer;
    public ulong Offset;
    public ulong Size;
    public Sampler? Sampler;
    public TextureView? TextureView;
}

public struct ProgrammableStageDescriptor
{
    public ShaderModule Module;
    public string EntryPoint;
}

public struct VertexState
{
    public ShaderModule Module;
    public string EntryPoint;
    public VertexBufferLayout[]? BufferLayouts;
    public ConstantEntry[]? ConstantEntries;
}

public struct ConstantEntry
{
    public string Key;
    public double Value;
}

public struct VertexBufferLayout
{
    public ulong ArrayStride;

    public VertexStepMode StepMode;

    public VertexAttribute[] Attributes;
}

public struct FragmentState
{
    public ShaderModule Module;
    public string EntryPoint;
    public ColorTargetState[] ColorTargets;
    public ConstantEntry[]? ConstantEntries;
}

public struct ColorTargetState
{
    public TextureFormat Format;

    public BlendState? BlendState;

    public ColorWriteMask WriteMask;
}
