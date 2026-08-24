using GameDotNet.Graphics.ILGPU.Abstractions;
using GameDotNet.Graphics.ILGPU.Tools;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;
using ILGPU.Util;
using ManagedCuda.BasicTypes;
using static ManagedCuda.BasicTypes.CUexternalSemaphoreHandleType;
using static ManagedCuda.DriverAPINativeMethods.ExternResources;

namespace GameDotNet.Graphics.ILGPU.Services;

internal class GpuInteropExtensionProvider : IAcceleratorExtensionProvider<GpuInteropExtensionBase>
{
    public GpuInteropExtensionBase CreateCPUExtension(CPUAccelerator accelerator) =>
        throw new NotImplementedException();

    public GpuInteropExtensionBase CreateCudaExtension(CudaAccelerator accelerator) =>
        new CudaGpuInteropExtension(accelerator);

    public GpuInteropExtensionBase CreateOpenCLExtension(CLAccelerator accelerator) =>
        throw new NotImplementedException();
}

internal abstract class GpuInteropExtensionBase : AcceleratorExtension
{
    public abstract MemoryBuffer GetInteropBuffer(nint handle, long byteSize, int elementSize);

    public abstract IExternalSemaphore ImportVulkanSemaphore(nint osHandle);

    public abstract void SignalSemaphoreAsync(
        IExternalSemaphore extSemaphore,
        AcceleratorStream stream,
        ulong value = 0
    );

    public abstract void WaitSemaphoreAsync(
        IExternalSemaphore extSemaphore,
        AcceleratorStream stream,
        ulong value = 0
    );
}

file sealed class CudaGpuInteropExtension(CudaAccelerator accelerator) : GpuInteropExtensionBase
{
    public override MemoryBuffer GetInteropBuffer(nint handle, long byteSize, int elementSize)
    {
        return new CudaInteropBuffer(
            accelerator,
            byteSize,
            elementSize,
            handle,
            OperatingSystem.IsWindows() //TODO: handle dx3d or other types of handles
                ? CUexternalMemoryHandleType.OpaqueWin32
                : CUexternalMemoryHandleType.OpaqueFD
        );
    }

    /// <summary>
    /// Imports the OS-level semaphore handle exported by Vulkan into a CUDA external semaphore.
    /// </summary>
    public override IExternalSemaphore ImportVulkanSemaphore(nint osHandle)
    {
        var isWindows = OperatingSystem.IsWindows();
        var handleDesc = new CudaExternalSemaphoreHandleDesc
        {
            type = isWindows ? TimelineSemaphoreWin32 : TimelineSemaphoreFD,
        };

        // Map the OS handle into the union struct provided by CudaSharp
        if (isWindows)
        {
            handleDesc.handle.win32.handle = osHandle;
        }
        else
        {
            handleDesc.handle.fd = (int)osHandle;
        }

        using var binding = accelerator.BindScoped();

        var cUexternalSemaphore = new CUexternalSemaphore();
        cuImportExternalSemaphore(ref cUexternalSemaphore, ref handleDesc).EnsureOk();

        return new CudaExternalSemaphore(accelerator, cUexternalSemaphore);
    }

    /// <summary>
    /// Enqueues a signal operation on the CUDA stream. Vulkan will wait on this
    /// before executing the CmdCopyBufferToImage operation.
    /// </summary>
    public override void SignalSemaphoreAsync(
        IExternalSemaphore extSemaphore,
        AcceleratorStream stream,
        ulong value = 0
    )
    {
        if (extSemaphore is not CudaExternalSemaphore cudaSemaphore)
        {
            throw new ArgumentException(
                "The semaphore must be an instance of CudaExternalSemaphore",
                nameof(extSemaphore)
            );
        }

        if (stream is not CudaStream cudaStream)
        {
            throw new ArgumentException(
                "The stream must be an instance of CudaStream",
                nameof(stream)
            );
        }

        var signalParams = new CudaExternalSemaphoreSignalParams
        {
            flags = 0, // Default flags for standard binary semaphores
            parameters = new() { fence = new() { value = value } },
        };

        cuSignalExternalSemaphoresAsync(
                [cudaSemaphore.Semaphore],
                [signalParams],
                1,
                new() { Pointer = cudaStream.StreamPtr }
            )
            .EnsureOk();
    }

    /// <summary>
    /// Enqueues a wait operation on the CUDA stream. CUDA will stall execution
    /// until Vulkan signals that it has finished presenting/copying the previous frame.
    /// </summary>
    public override void WaitSemaphoreAsync(
        IExternalSemaphore extSemaphore,
        AcceleratorStream stream,
        ulong value = 0
    )
    {
        if (extSemaphore is not CudaExternalSemaphore cudaSemaphore)
        {
            throw new ArgumentException(
                "The semaphore must be an instance of CudaExternalSemaphore",
                nameof(extSemaphore)
            );
        }

        if (stream is not CudaStream cudaStream)
        {
            throw new ArgumentException(
                "The stream must be an instance of CudaStream",
                nameof(stream)
            );
        }

        var waitParams = new CudaExternalSemaphoreWaitParams
        {
            parameters = new() { fence = new() { value = value } },
            flags = 0,
        };

        cuWaitExternalSemaphoresAsync(
                [cudaSemaphore.Semaphore],
                [waitParams],
                1,
                new() { Pointer = cudaStream.StreamPtr }
            )
            .EnsureOk();
    }
}
