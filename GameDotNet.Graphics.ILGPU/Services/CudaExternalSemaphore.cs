using GameDotNet.Graphics.ILGPU.Abstractions;
using GameDotNet.Graphics.ILGPU.Tools;
using ILGPU.Runtime;
using ManagedCuda.BasicTypes;
using static ManagedCuda.DriverAPINativeMethods.ExternResources;

namespace GameDotNet.Graphics.ILGPU.Services;

internal sealed class CudaExternalSemaphore(Accelerator accelerator, CUexternalSemaphore semaphore)
    : AcceleratorObject(accelerator),
        IExternalSemaphore
{
    public CUexternalSemaphore Semaphore { get; } = semaphore;

    protected override void DisposeAcceleratorObject(bool disposing)
    {
        if (disposing)
        {
            cuDestroyExternalSemaphore(Semaphore).EnsureOk();
        }
    }
}
