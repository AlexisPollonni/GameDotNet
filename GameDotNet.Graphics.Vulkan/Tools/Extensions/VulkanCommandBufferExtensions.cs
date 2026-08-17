using System.Runtime.CompilerServices;
using ByteSizeLib;
using GameDotNet.Graphics.Vulkan.Abstractions;
using GameDotNet.Graphics.Vulkan.Wrappers;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace GameDotNet.Graphics.Vulkan.Tools.Extensions;

public static class VulkanCommandBufferExtensions
{
    extension<TCommandBuffer>(TCommandBuffer commandBuffer)
        where TCommandBuffer : IVulkanWrapper<CommandBuffer>, allows ref struct
    {
        public TCommandBuffer TransitionLayout<TImage>(
            TImage image,
            ImageLayout sourceLayout,
            AccessFlags sourceAccessMask,
            ImageLayout destinationLayout,
            AccessFlags destinationAccessMask,
            uint mipLevels
        )
            where TImage : IVulkanImage
        {
            var subresourceRange = new ImageSubresourceRange(
                ImageAspectFlags.ColorBit,
                0,
                mipLevels,
                0,
                1
            );

            var barrier = new ImageMemoryBarrier
            {
                SType = StructureType.ImageMemoryBarrier,
                SrcAccessMask = sourceAccessMask,
                DstAccessMask = destinationAccessMask,
                OldLayout = sourceLayout,
                NewLayout = destinationLayout,
                SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
                DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                Image = image.Underlying,
                SubresourceRange = subresourceRange,
            };

            commandBuffer.Context.Api.CmdPipelineBarrier(
                commandBuffer.Underlying,
                PipelineStageFlags.AllCommandsBit,
                PipelineStageFlags.AllCommandsBit,
                0,
                0,
                [],
                0,
                [],
                1,
                [barrier]
            );

            image.CurrentLayout = destinationLayout;
            image.CurrentAccessFlags = destinationAccessMask;

            return commandBuffer;
        }

        public TCommandBuffer TransitionLayout<TImage>(
            TImage image,
            ImageLayout fromLayout,
            AccessFlags fromAccessFlags,
            ImageLayout destinationLayout,
            AccessFlags destinationAccessFlags
        )
            where TImage : IVulkanImage
        {
            ref readonly var createInfo = ref image.CreateInfo;

            return commandBuffer.TransitionLayout(
                image,
                fromLayout,
                fromAccessFlags,
                destinationLayout,
                destinationAccessFlags,
                createInfo.MipLevels
            );
        }

        public TCommandBuffer TransitionLayout<TImage>(
            TImage image,
            ImageLayout destinationLayout,
            AccessFlags destinationAccessFlags
        )
            where TImage : IVulkanImage
        {
            return commandBuffer.TransitionLayout(
                image,
                image.CurrentLayout,
                image.CurrentAccessFlags,
                destinationLayout,
                destinationAccessFlags
            );
        }

        public TCommandBuffer TransitionLayout<TImage>(TImage image, ImageLayout destinationLayout)
            where TImage : IVulkanImage
        {
            return commandBuffer.TransitionLayout(
                image,
                destinationLayout,
                image.CurrentAccessFlags
            );
        }

        /// <summary>
        /// Copies pixel data from an Optimal Vulkan Image into a linear Vulkan Buffer.
        /// Returns image to the previous layout after copy.
        /// Image must support TransferSrcOptimal layout.
        /// </summary>
        public TCommandBuffer CopyImageToBuffer<TImage, TBuffer>(TImage srcImage, TBuffer dstBuffer)
            where TImage : IVulkanImage
            where TBuffer : IVulkanWrapper<Buffer>
        {
            var oldLayout = srcImage.CurrentLayout;

            if (oldLayout is ImageLayout.Undefined)
            {
                throw new InvalidOperationException($"Image layout must not be {oldLayout}");
            }

            if (dstBuffer is not VulkanBuffer dstVkBuffer) //TODO: change this when buffer is refactored
            {
                throw new InvalidOperationException("Destination buffer must be a VulkanBuffer");
            }

            var bpp = srcImage.BytesPerPixel;
            var requiredSize = ByteSize.FromBytes(
                srcImage.Extent.Width * srcImage.Extent.Height * srcImage.Extent.Depth * bpp.Bytes
            );

            var dstBufferSize = ByteSize.FromBytes(dstVkBuffer.Allocation.Size);

            if (dstBufferSize < requiredSize)
            {
                throw new InvalidOperationException(
                    $"Destination buffer is too small! Required: {requiredSize} bytes, Available: {dstBufferSize} bytes."
                );
            }

            commandBuffer.TransitionLayout(srcImage, ImageLayout.TransferSrcOptimal);

            // 2. Define the tightly packed copy region
            var copyRegion = new BufferImageCopy
            {
                BufferOffset = 0,
                BufferRowLength = 0, // 0 indicates tightly packed
                BufferImageHeight = 0, // 0 indicates tightly packed
                ImageSubresource = new()
                {
                    AspectMask = ImageAspectFlags.ColorBit, //TODO: consider supporting other aspect flags such as Depth if needed
                    MipLevel = 0,
                    BaseArrayLayer = 0,
                    LayerCount = 1,
                },
                ImageOffset = new(0, 0, 0),
                ImageExtent = srcImage.Extent,
            };

            // 3. Record the copy command
            commandBuffer.Context.Api.CmdCopyImageToBuffer(
                commandBuffer.Underlying,
                srcImage.Underlying,
                ImageLayout.TransferSrcOptimal,
                dstBuffer.Underlying,
                [copyRegion]
            );

            return commandBuffer.TransitionLayout(srcImage, oldLayout);
        }

        /// <summary>
        /// Copies linear pixel data from a Vulkan Buffer into an Optimal Vulkan Image.
        /// Handles image layout transitions automatically.
        /// Image must support TransferDstOptimal layout.
        /// </summary>
        public TCommandBuffer CopyBufferToImage<TBuffer, TImage>(TBuffer srcBuffer, TImage dstImage)
            where TBuffer : IVulkanWrapper<Buffer>
            where TImage : IVulkanImage
        {
            if (srcBuffer is not VulkanBuffer srcVkBuffer) //TODO: change this when buffer is refactored
            {
                throw new InvalidOperationException("Destination buffer must be a VulkanBuffer");
            }

            var bpp = dstImage.BytesPerPixel;
            var requiredSize = ByteSize.FromBytes(
                dstImage.Extent.Width * dstImage.Extent.Height * dstImage.Extent.Depth * bpp.Bytes
            );

            var srcBufferSize = ByteSize.FromBytes(srcVkBuffer.Allocation.Size);
            if (srcBufferSize < requiredSize)
            {
                throw new InvalidOperationException(
                    $"Source buffer does not contain enough data! Required: {requiredSize} bytes, Available: {srcBufferSize} bytes."
                );
            }

            // 1. Transition image layout to TRANSFER_DST_OPTIMAL
            var originalLayout = dstImage.CurrentLayout;
            commandBuffer.TransitionLayout(dstImage, ImageLayout.TransferDstOptimal);

            // 2. Define the tightly packed copy region
            var copyRegion = new BufferImageCopy
            {
                BufferOffset = 0,
                BufferRowLength = 0, // 0 indicates tightly packed (row pitch == width)
                BufferImageHeight = 0, // 0 indicates tightly packed (height == height)
                ImageSubresource = new()
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    MipLevel = 0,
                    BaseArrayLayer = 0,
                    LayerCount = 1,
                },
                ImageOffset = new(0, 0, 0),
                ImageExtent = dstImage.Extent,
            };

            // 3. Record the copy command
            commandBuffer.Context.Api.CmdCopyBufferToImage(
                commandBuffer.Underlying,
                srcBuffer.Underlying,
                dstImage.Underlying,
                ImageLayout.TransferDstOptimal,
                [copyRegion]
            );

            // 4. Transition image back to original layout
            return originalLayout is not ImageLayout.Undefined
                ? commandBuffer.TransitionLayout(dstImage, originalLayout)
                : commandBuffer;
        }
    }
}
