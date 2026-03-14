using GameDotNet.Graphics.Vulkan.Wrappers;
using Silk.NET.Core;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace GameDotNet.Graphics.Vulkan.Tools.Extensions;

public static class VulkanShaderObjectExtensions
{
    extension(VulkanDevice device)
    {
        public unsafe ShaderEXT CreateShaderObject(
            ShaderStageFlags stage,
            string name,
            ReadOnlyMemory<byte> spirvCode,
            ShaderCreateFlagsEXT flags = ShaderCreateFlagsEXT.None
        )
        {
            using var pCode = spirvCode.Pin();
            Span<ShaderEXT> outShaders = [default];
            using var nameMem = name.ToGlobalMemory();
            var createInfo = new ShaderCreateInfoEXT
            {
                CodeType = ShaderCodeTypeEXT.SpirvExt,
                Stage = stage,
                Flags = flags,
                CodeSize = (uint)spirvCode.Length,
                PCode = pCode.Pointer,
                PName = (byte*)nameMem.Handle,
                //TODO: descriptor set and push constant
                //TODO: pnext
            };
            device
                .ShaderObjectExt.CreateShaders(
                    device,
                    1u,
                    [createInfo],
                    [device.Context.Callbacks.Handle],
                    outShaders
                )
                .ThrowOnError();
            return outShaders[0];
        }

        public void DestroyShaderObject(ShaderEXT shader)
        {
            device.ShaderObjectExt.DestroyShader(
                device,
                shader,
                in device.Context.Callbacks.Handle
            );
        }

        public unsafe byte[] GetShaderBinaryData(ShaderEXT shader)
        {
            nuint dataSize = 0;
            device
                .ShaderObjectExt.GetShaderBinaryData(device, shader, ref dataSize, null)
                .ThrowOnError();
            var data = new byte[dataSize];
            fixed (byte* pData = data)
            {
                device
                    .ShaderObjectExt.GetShaderBinaryData(device, shader, ref dataSize, pData)
                    .ThrowOnError();
            }
            return data;
        }
    }
    extension(VulkanCommandBufferPool.VulkanCommandBuffer commandBuffer)
    {
        // --- Shader binding ---
        public void BindShaders(
            ReadOnlySpan<ShaderStageFlags> stages,
            ReadOnlySpan<ShaderEXT> shaders
        )
        {
            commandBuffer.ShaderObjectExt.CmdBindShaders(commandBuffer, stages, shaders);
        }

        // --- Vertex input ---
        public void SetVertexInput(
            ReadOnlySpan<VertexInputBindingDescription2EXT> vertexBindings,
            ReadOnlySpan<VertexInputAttributeDescription2EXT> vertexAttributes
        )
        {
            commandBuffer.ShaderObjectExt.CmdSetVertexInput(
                commandBuffer,
                vertexBindings,
                vertexAttributes
            );
        }

        public void BindVertexBuffers2(
            uint firstBinding,
            ReadOnlySpan<Buffer> buffers,
            ReadOnlySpan<ulong> offsets,
            ReadOnlySpan<ulong> sizes,
            ReadOnlySpan<ulong> strides
        )
        {
            commandBuffer.ShaderObjectExt.CmdBindVertexBuffers2(
                (CommandBuffer)commandBuffer,
                firstBinding,
                buffers,
                offsets,
                sizes,
                strides
            );
        }

        // --- Input assembly ---
        public PrimitiveTopology PrimitiveTopology
        {
            set => commandBuffer.ShaderObjectExt.CmdSetPrimitiveTopology(commandBuffer, value);
        }
        public bool PrimitiveRestartEnable
        {
            set => commandBuffer.ShaderObjectExt.CmdSetPrimitiveRestartEnable(commandBuffer, value);
        }

        // --- Tessellation ---
        public uint PatchControlPoints
        {
            set => commandBuffer.ShaderObjectExt.CmdSetPatchControlPoints(commandBuffer, value);
        }
        public TessellationDomainOrigin TessellationDomainOrigin
        {
            set =>
                commandBuffer.ShaderObjectExt.CmdSetTessellationDomainOrigin(commandBuffer, value);
        }

        // --- Viewport / Scissor ---
        public ReadOnlySpan<Viewport> Viewports
        {
            set => commandBuffer.ShaderObjectExt.CmdSetViewportWithCount(commandBuffer, value);
        }

        public ReadOnlySpan<Rect2D> Scissors
        {
            set => commandBuffer.ShaderObjectExt.CmdSetScissorWithCount(commandBuffer, value);
        }

        // --- Rasterization ---
        public CullModeFlags CullMode
        {
            set => commandBuffer.ShaderObjectExt.CmdSetCullMode(commandBuffer, value);
        }
        public FrontFace FrontFace
        {
            set => commandBuffer.ShaderObjectExt.CmdSetFrontFace(commandBuffer, value);
        }
        public PolygonMode PolygonMode
        {
            set => commandBuffer.ShaderObjectExt.CmdSetPolygonMode(commandBuffer, value);
        }
        public bool RasterizerDiscardEnable
        {
            set =>
                commandBuffer.ShaderObjectExt.CmdSetRasterizerDiscardEnable(commandBuffer, value);
        }
        public bool DepthBiasEnable
        {
            set => commandBuffer.ShaderObjectExt.CmdSetDepthBiasEnable(commandBuffer, value);
        }
        public bool DepthClampEnable
        {
            set => commandBuffer.ShaderObjectExt.CmdSetDepthClampEnable(commandBuffer, value);
        }

        public void SetDepthClampRange(DepthClampModeEXT mode, in DepthClampRangeEXT range)
        {
            commandBuffer.ShaderObjectExt.CmdSetDepthClampRange(commandBuffer, mode, in range);
        }

        public bool DepthClipEnable
        {
            set => commandBuffer.ShaderObjectExt.CmdSetDepthClipEnable(commandBuffer, value);
        }
        public bool DepthClipNegativeOneToOne
        {
            set =>
                commandBuffer.ShaderObjectExt.CmdSetDepthClipNegativeOneToOne(commandBuffer, value);
        }
        public ProvokingVertexModeEXT ProvokingVertexMode
        {
            set => commandBuffer.ShaderObjectExt.CmdSetProvokingVertexMode(commandBuffer, value);
        }
        public uint RasterizationStream
        {
            set => commandBuffer.ShaderObjectExt.CmdSetRasterizationStream(commandBuffer, value);
        }
        public ConservativeRasterizationModeEXT ConservativeRasterizationMode
        {
            set =>
                commandBuffer.ShaderObjectExt.CmdSetConservativeRasterizationMode(
                    commandBuffer,
                    value
                );
        }
        public float ExtraPrimitiveOverestimationSize
        {
            set =>
                commandBuffer.ShaderObjectExt.CmdSetExtraPrimitiveOverestimationSize(
                    commandBuffer,
                    value
                );
        }

        // --- Line rasterization ---
        public LineRasterizationModeEXT LineRasterizationMode
        {
            set => commandBuffer.ShaderObjectExt.CmdSetLineRasterizationMode(commandBuffer, value);
        }
        public bool LineStippleEnable
        {
            set => commandBuffer.ShaderObjectExt.CmdSetLineStippleEnable(commandBuffer, value);
        }

        // --- Multisampling ---
        public SampleCountFlags RasterizationSamples
        {
            set => commandBuffer.ShaderObjectExt.CmdSetRasterizationSamples(commandBuffer, value);
        }

        public void SetSampleMask(SampleCountFlags samples, in uint sampleMask)
        {
            commandBuffer.ShaderObjectExt.CmdSetSampleMask(commandBuffer, samples, in sampleMask);
        }

        public bool AlphaToCoverageEnable
        {
            set => commandBuffer.ShaderObjectExt.CmdSetAlphaToCoverageEnable(commandBuffer, value);
        }
        public bool AlphaToOneEnable
        {
            set => commandBuffer.ShaderObjectExt.CmdSetAlphaToOneEnable(commandBuffer, value);
        }
        public bool SampleLocationsEnable
        {
            set => commandBuffer.ShaderObjectExt.CmdSetSampleLocationsEnable(commandBuffer, value);
        }

        // --- Depth / Stencil ---
        public bool DepthTestEnable
        {
            set => commandBuffer.ShaderObjectExt.CmdSetDepthTestEnable(commandBuffer, value);
        }
        public bool DepthWriteEnable
        {
            set => commandBuffer.ShaderObjectExt.CmdSetDepthWriteEnable(commandBuffer, value);
        }
        public CompareOp DepthCompareOp
        {
            set => commandBuffer.ShaderObjectExt.CmdSetDepthCompareOp(commandBuffer, value);
        }
        public bool DepthBoundsTestEnable
        {
            set => commandBuffer.ShaderObjectExt.CmdSetDepthBoundsTestEnable(commandBuffer, value);
        }
        public bool StencilTestEnable
        {
            set => commandBuffer.ShaderObjectExt.CmdSetStencilTestEnable(commandBuffer, value);
        }

        public void SetStencilOp(
            StencilFaceFlags faceMask,
            StencilOp failOp,
            StencilOp passOp,
            StencilOp depthFailOp,
            CompareOp compareOp
        )
        {
            commandBuffer.ShaderObjectExt.CmdSetStencilOp(
                commandBuffer,
                faceMask,
                failOp,
                passOp,
                depthFailOp,
                compareOp
            );
        }

        // --- Color blend ---
        public void SetColorBlendEnable(uint firstAttachment, ReadOnlySpan<Bool32> enables)
        {
            commandBuffer.ShaderObjectExt.CmdSetColorBlendEnable(
                commandBuffer,
                firstAttachment,
                enables
            );
        }

        public void SetColorBlendEquation(
            uint firstAttachment,
            ReadOnlySpan<ColorBlendEquationEXT> equations
        )
        {
            commandBuffer.ShaderObjectExt.CmdSetColorBlendEquation(
                commandBuffer,
                firstAttachment,
                equations
            );
        }

        public void SetColorWriteMask(
            uint firstAttachment,
            ReadOnlySpan<ColorComponentFlags> writeMasks
        )
        {
            commandBuffer.ShaderObjectExt.CmdSetColorWriteMask(
                commandBuffer,
                firstAttachment,
                writeMasks
            );
        }

        public void SetColorBlendAdvanced(
            uint firstAttachment,
            ReadOnlySpan<ColorBlendAdvancedEXT> blendAdvanced
        )
        {
            commandBuffer.ShaderObjectExt.CmdSetColorBlendAdvance(
                commandBuffer,
                firstAttachment,
                blendAdvanced
            );
        }

        public bool LogicOpEnable
        {
            set => commandBuffer.ShaderObjectExt.CmdSetLogicOpEnable(commandBuffer, value);
        }
        public LogicOp LogicOp
        {
            set => commandBuffer.ShaderObjectExt.CmdSetLogicOp(commandBuffer, value);
        }
    }
}
