namespace GameDotNet.Graphics.WGPU.Wrappers;

public delegate void RequestAdapterCallback(RequestAdapterStatus status, Adapter adapter, string message);

public delegate void RequestDeviceCallback(RequestDeviceStatus status, Device device, string message);

public delegate void CreateComputePipelineAsyncCallback(CreatePipelineAsyncStatus status, ComputePipeline pipeline,
                                                        string message);

public delegate void ErrorCallback(ErrorType type, string message);

public delegate void LoggingCallback(LogLevel type, string message);

public delegate void DeviceLostCallback(DeviceLostReason reason, string message);

public delegate void BufferMapCallback(BufferMapAsyncStatus status);

public delegate void CompilationInfoCallback(CompilationInfoRequestStatus status,
                                             ReadOnlySpan<CompilationMessage> messages);

public delegate void QueueWorkDoneCallback(QueueWorkDoneStatus status);

public readonly record struct AdapterProperties(
    uint VendorId = 0,
    string VendorName = "",
    string Architecture = "",
    uint DeviceId = 0,
    string Name = "",
    string DriverDescription = "",
    AdapterType AdapterType = AdapterType.Unknown,
    BackendType BackendType = BackendType.Undefined);

public struct RequiredLimits
{
    public Limits Limits;
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


public struct IndexedIndirect
{
    public uint IndexCount;
    public uint InstanceCount;
    public uint FirstIndex;
    public uint BaseVertex;
    public uint FirstInstance;
}