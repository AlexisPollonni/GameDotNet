using ByteSizeLib;
using GameDotNet.Graphics.Vulkan.Wrappers;
using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan.Tools.Extensions;

public static class WrapperExtensions
{
    extension(SelectedPhysDevice physDevice) //TODO: refactor when device flow implements wrappers
    {
        public unsafe Guid DeviceUuid
        {
            get
            {
                var context = physDevice.Device.Context;

                PhysicalDeviceProperties2
                    .Chain(out var prop2)
                    .AddNext(out PhysicalDeviceIDProperties idProperties);

                //IMPORTANT: we do not use the out or span overloads as they recreate a new PhysicalDeviceProperties2 and override our chain. This is probably a bug in the bindings.
                context.Api.GetPhysicalDeviceProperties2(physDevice.Device, &prop2);

                if (idProperties.DeviceUuid is null)
                    return Guid.Empty;

                var deviceUuid = new ReadOnlySpan<byte>(idProperties.DeviceUuid, (int)Vk.UuidSize);

                // ReSharper disable once UseCollectionExpression
                // We need this to avoid the on stack prop2 to be cleaned up when the property returns
                return new(deviceUuid, true); //Vulkan UUIDs are big endian
            }
        }
    }

    extension<TVulkanImage>(TVulkanImage image)
        where TVulkanImage : IVulkanImage
    {
        public ref readonly ImageCreateInfo CreateInfo => ref image.InfoChain.HeadRef;

        public ByteSize BytesPerPixel =>
            ByteSize.FromBytes(
                image.Format switch
                {
                    Format.R8Unorm or Format.R8SNorm or Format.R8Uint or Format.R8Sint => 1,

                    Format.R8G8Unorm or Format.R8G8SNorm or Format.R8G8Uint or Format.R8G8Sint => 2,
                    Format.R16Unorm
                    or Format.R16SNorm
                    or Format.R16Uint
                    or Format.R16Sint
                    or Format.R16Sfloat => 2,

                    Format.B8G8R8A8Unorm
                    or Format.B8G8R8A8Srgb
                    or Format.R8G8B8A8Unorm
                    or Format.R8G8B8A8Srgb => 4,
                    Format.R8G8B8A8Uint or Format.R8G8B8A8Sint => 4,
                    Format.R16G16Unorm
                    or Format.R16G16SNorm
                    or Format.R16G16Uint
                    or Format.R16G16Sint
                    or Format.R16G16Sfloat => 4,
                    Format.R32Uint or Format.R32Sint or Format.R32Sfloat => 4,
                    Format.B10G11R11UfloatPack32 => 4,

                    Format.R16G16B16A16Unorm
                    or Format.R16G16B16A16SNorm
                    or Format.R16G16B16A16Uint
                    or Format.R16G16B16A16Sint
                    or Format.R16G16B16A16Sfloat => 8,
                    Format.R32G32Uint or Format.R32G32Sint or Format.R32G32Sfloat => 8,

                    Format.R32G32B32A32Uint
                    or Format.R32G32B32A32Sint
                    or Format.R32G32B32A32Sfloat => 16,

                    // Add Depth formats if you intend to copy depth buffers
                    Format.D16Unorm => 2,
                    Format.D32Sfloat => 4,
                    Format.D24UnormS8Uint => 4,

                    _ => throw new NotSupportedException(
                        $"Format {image.Format} size calculation is not implemented."
                    ),
                }
            );
    }
}
