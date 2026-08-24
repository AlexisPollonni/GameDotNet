using GameDotNet.Graphics.ILGPU.Abstractions;
using GameDotNet.Graphics.ILGPU.Models;
using GameDotNet.Graphics.ILGPU.Services;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;
using ManagedCuda.BasicTypes;
using Silk.NET.OpenCL;
using static ManagedCuda.DriverAPINativeMethods.DeviceManagement;

namespace GameDotNet.Graphics.ILGPU.Tools;

public static class IlGpuExtensions
{
    extension(Device device)
    {
        public unsafe Guid GetUuid()
        {
            switch (device.AcceleratorType)
            {
                case AcceleratorType.CPU:
                    return Guid.Empty; // The cpu device does not have a Uuid
                case AcceleratorType.Cuda:
                    var cudaDevice = (CudaDevice)device;

                    var nvcudaDevice = new CUdevice(cudaDevice.DeviceId);

                    var uuid = new CUuuid();
                    cuDeviceGetUuid(ref uuid, nvcudaDevice).EnsureOk();

                    return new(uuid.bytes, true); //CUDA UUIDs are big endian
                case AcceleratorType.OpenCL:
                    var clDevice = (CLDevice)device;

                    Span<byte> clUuidSpan = stackalloc byte[CL.UuidSizeKhr];
                    var result = CL.GetApi()
                        .GetDeviceInfo(
                            clDevice.DeviceId,
                            DeviceInfo.UuidKhr,
                            CL.UuidSizeKhr,
                            clUuidSpan,
                            []
                        );

                    return result != 0 ? Guid.Empty : new(clUuidSpan, true); //OpenCL UUIDs are big endian

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    extension(Accelerator accelerator)
    {
        public Accelerator AddGpuInterop()
        {
            var extension = accelerator.CreateExtension<
                GpuInteropExtensionBase,
                GpuInteropExtensionProvider
            >(new());

            accelerator.RegisterExtension(extension);
            return accelerator;
        }

        public MemoryBuffer ImportInteropBuffer(nint handle, long byteSize, int elementSize)
        {
            var ext = accelerator.GetExtension<GpuInteropExtensionBase>();

            return ext.GetInteropBuffer(handle, byteSize, elementSize);
        }

        public IExternalSemaphore ImportInteropSemaphore(nint handle)
        {
            var ext = accelerator.GetExtension<GpuInteropExtensionBase>();

            return ext.ImportVulkanSemaphore(handle);
        }
    }

    extension(AcceleratorStream stream)
    {
        public void WaitTimelineSemaphoreAsync(IExternalSemaphore semaphore, ulong value)
        {
            var ext = stream.Accelerator.GetExtension<GpuInteropExtensionBase>();

            ext.WaitSemaphoreAsync(semaphore, stream, value);
        }

        public void SignalTimelineSemaphoreAsync(IExternalSemaphore semaphore, ulong value)
        {
            var ext = stream.Accelerator.GetExtension<GpuInteropExtensionBase>();

            ext.SignalSemaphoreAsync(semaphore, stream, value);
        }
    }
}

public static class FrameGraphExtensions
{
    extension(IFrameGraphBuilder builder)
    {
        public IFrameGraphBuilder CreateBuffer2D<T>(
            Index2D extent,
            out BufferHandle<T, Index2D, Stride2D.DenseX> handle
        )
            where T : unmanaged
        {
            var stride = Stride2D.DenseX.FromExtent(extent);

            return builder.CreateBuffer(
                extent,
                stride,
                buffer => buffer.AsRawArrayView().Cast<T>().AsDense().As2DDenseXView(extent),
                out handle
            );
        }

        public IFrameGraphBuilder Read2D<T>(BufferHandle<T, Index2D, Stride2D.DenseX> handle)
            where T : unmanaged
        {
            return builder.Read(handle);
        }
    }

    public static ArrayView2D<T, Stride2D.DenseX> GetView2D<T>(
        this IFrameGraphContext context,
        BufferHandle<T, Index2D, Stride2D.DenseX> handle
    )
        where T : unmanaged
    {
        return context.GetView<T, Index2D, Stride2D.DenseX, ArrayView2D<T, Stride2D.DenseX>>(
            handle
        );
    }
}
