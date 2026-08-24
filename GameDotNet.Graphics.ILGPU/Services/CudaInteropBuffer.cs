using GameDotNet.Graphics.ILGPU.Tools;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using ManagedCuda.BasicTypes;
using Silk.NET.Vulkan;
using static ManagedCuda.BasicTypes.CUexternalMemoryHandleType;
using static ManagedCuda.DriverAPINativeMethods.ExternResources;
using static ManagedCuda.DriverAPINativeMethods.MemoryManagement;

namespace GameDotNet.Graphics.ILGPU.Services;

internal class CudaInteropBuffer : MemoryBuffer
{
    private readonly CudaAccelerator _accelerator;
    private readonly CUexternalMemory _externalMemory;

    public CudaInteropBuffer(
        CudaAccelerator accelerator,
        long length,
        int elementSize,
        nint handle,
        CUexternalMemoryHandleType type
    )
        : base(accelerator, length, elementSize)
    {
        _accelerator = accelerator;
        _externalMemory = ImportExternalMemory(handle, (nuint)LengthInBytes, type);

        var bufferDesc = new CudaExternalMemoryBufferDesc
        {
            offset = 0,
            size = (ulong)LengthInBytes,
            flags = 0,
        };

        using var scope = _accelerator.BindScoped();

        var outPtr = new CUdeviceptr();
        cuExternalMemoryGetMappedBuffer(ref outPtr, _externalMemory, ref bufferDesc).EnsureOk();

        NativePtr = outPtr.Pointer;
    }

    protected override void DisposeAcceleratorObject(bool disposing)
    {
        cuMemFree_v2(new(NativePtr)).EnsureOk();
        cuDestroyExternalMemory(_externalMemory);
        NativePtr = 0;
    }

    protected override void MemSet(
        AcceleratorStream stream,
        byte value,
        in ArrayView<byte> targetView
    ) => CudaMemoryBuffer.CudaMemSet(stream as CudaStream, value, targetView);

    protected override void CopyFrom(
        AcceleratorStream stream,
        in ArrayView<byte> sourceView,
        in ArrayView<byte> targetView
    ) => CudaMemoryBuffer.CudaCopy(stream as CudaStream, sourceView, targetView);

    /// <inheritdoc/>
    protected override void CopyTo(
        AcceleratorStream stream,
        in ArrayView<byte> sourceView,
        in ArrayView<byte> targetView
    ) => CudaMemoryBuffer.CudaCopy(stream as CudaStream, sourceView, targetView);

    internal static CUexternalMemoryHandleType ToCudaMemoryType(ExternalMemoryHandleTypeFlags flags)
    { //TODO: move to extensions and use to infer vk memory type
        return flags switch
        {
            ExternalMemoryHandleTypeFlags.OpaqueFDBit => OpaqueFD,
            ExternalMemoryHandleTypeFlags.OpaqueWin32Bit => OpaqueWin32,
            ExternalMemoryHandleTypeFlags.OpaqueWin32KmtBit => OpaqueWin32KMT,
            ExternalMemoryHandleTypeFlags.D3D11TextureBit => D3D11Resource,
            ExternalMemoryHandleTypeFlags.D3D11TextureKmtBit => D3D11ResourceKMT,
            ExternalMemoryHandleTypeFlags.D3D12HeapBit => D3D12Heap,
            ExternalMemoryHandleTypeFlags.D3D12ResourceBit => D3D12Resource,
            ExternalMemoryHandleTypeFlags.SciBufBitNV => NvSciBuf,
            ExternalMemoryHandleTypeFlags.DmaBufBitExt => DMABufFD,
            _ => throw new InvalidOperationException($"Unsupported handle type: {flags}"),
        };
    }

    private CUexternalMemory ImportExternalMemory(
        nint handle,
        nuint size,
        CUexternalMemoryHandleType type
    )
    {
        HandleUnion handleDesc = type switch
        {
            D3D11ResourceKMT
            or OpaqueWin32KMT
            or D3D11Resource
            or D3D12Heap
            or D3D12Resource
            or OpaqueWin32 => new() { win32 = new() { handle = handle } },
            OpaqueFD => new() { fd = (int)handle },
            NvSciBuf => new() { nvSciBufObject = handle },
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };

        var desc = new CudaExternalMemoryHandleDesc
        {
            handle = handleDesc,
            type = type,
            size = size,
            flags = type is D3D11Resource or D3D11ResourceKMT or D3D12Resource
                ? CudaExternalMemory.Dedicated
                : CudaExternalMemory.Nothing,
        };
        using var bindScope = _accelerator.BindScoped();

        var external = new CUexternalMemory();
        cuImportExternalMemory(ref external, ref desc).EnsureOk();
        return external;
    }
}
