using GameDotNet.Graphics.Vulkan.Abstractions;
using GameDotNet.Graphics.Vulkan.Wrappers;
using Silk.NET.Core;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace GameDotNet.Graphics.Vulkan.Tools.Extensions;

public static class VulkanShaderObjectExtensions
{
    extension<TDevice>(TDevice device)
        where TDevice : IVulkanWrapper<Device>
    {
        public unsafe ShaderEXT CreateShaderObject(
            string name,
            ReadOnlySpan<byte> spirvCode,
            ShaderStageFlags stage,
            ShaderStageFlags nextStage,
            ReadOnlySpan<PushConstantRange> pushConstants,
            ReadOnlySpan<DescriptorSetLayout> descriptorSetLayouts,
            ShaderCreateFlagsEXT flags = ShaderCreateFlagsEXT.None
        )
        {
            Span<ShaderEXT> outShaders = [default];
            fixed (void* pCode = spirvCode)
            fixed (PushConstantRange* pPushConstants = pushConstants)
            fixed (DescriptorSetLayout* pSetLayouts = descriptorSetLayouts)
            {
                using var nameMem = name.ToGlobalMemory();
                var createInfo = new ShaderCreateInfoEXT(
                    codeType: ShaderCodeTypeEXT.SpirvExt,
                    stage: stage,
                    nextStage: nextStage,
                    flags: flags,
                    codeSize: (nuint)spirvCode.Length,
                    pCode: pCode,
                    pName: (byte*)nameMem.Handle,
                    pPushConstantRanges: pPushConstants,
                    pushConstantRangeCount: (uint)pushConstants.Length,
                    pSetLayouts: pSetLayouts,
                    setLayoutCount: (uint)descriptorSetLayouts.Length
                ); //TODO: PNext, specializations?

                device
                    .ShaderObjectExt.CreateShaders(
                        device.Underlying,
                        1u,
                        [createInfo],
                        [device.Context.Callbacks.Underlying],
                        outShaders
                    )
                    .ThrowOnError();
            }

            return outShaders[0];
        }

        public void DestroyShaderObject(ShaderEXT shader)
        {
            device.ShaderObjectExt.DestroyShader(
                device.Underlying,
                shader,
                in device.Context.Callbacks.Underlying
            );
        }

        public unsafe byte[] GetShaderBinaryData(ShaderEXT shader)
        {
            nuint dataSize = 0;
            device
                .ShaderObjectExt.GetShaderBinaryData(device.Underlying, shader, ref dataSize, null)
                .ThrowOnError();
            var data = new byte[dataSize];
            fixed (byte* pData = data)
            {
                device
                    .ShaderObjectExt.GetShaderBinaryData(
                        device.Underlying,
                        shader,
                        ref dataSize,
                        pData
                    )
                    .ThrowOnError();
            }

            return data;
        }
    }

    extension<TCommandBuffer>(TCommandBuffer commandBuffer)
        where TCommandBuffer : IVulkanWrapper<CommandBuffer>
    {
        // --- Shader binding ---
        public void BindShaders(
            ReadOnlySpan<ShaderStageFlags> stages,
            ReadOnlySpan<ShaderEXT> shaders
        )
        {
            commandBuffer.ShaderObjectExt.CmdBindShaders(commandBuffer.Underlying, stages, shaders);
        }

        // --- Vertex input ---
        public void SetVertexInput(
            ReadOnlySpan<VertexInputBindingDescription2EXT> vertexBindings,
            ReadOnlySpan<VertexInputAttributeDescription2EXT> vertexAttributes
        )
        {
            commandBuffer.ShaderObjectExt.CmdSetVertexInput(
                commandBuffer.Underlying,
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
                commandBuffer.Underlying,
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
            set =>
                commandBuffer.ShaderObjectExt.CmdSetPrimitiveTopology(
                    commandBuffer.Underlying,
                    value
                );
        }

        public bool PrimitiveRestartEnable
        {
            set =>
                commandBuffer.ShaderObjectExt.CmdSetPrimitiveRestartEnable(
                    commandBuffer.Underlying,
                    value
                );
        }

        // --- Tessellation ---
        public uint PatchControlPoints
        {
            set =>
                commandBuffer.ShaderObjectExt.CmdSetPatchControlPoints(
                    commandBuffer.Underlying,
                    value
                );
        }

        public TessellationDomainOrigin TessellationDomainOrigin
        {
            set =>
                commandBuffer.ShaderObjectExt.CmdSetTessellationDomainOrigin(
                    commandBuffer.Underlying,
                    value
                );
        }

        // --- Viewport / Scissor ---
        public ReadOnlySpan<Viewport> Viewports
        {
            set =>
                commandBuffer.ShaderObjectExt.CmdSetViewportWithCount(
                    commandBuffer.Underlying,
                    value
                );
        }

        public ReadOnlySpan<Rect2D> Scissors
        {
            set =>
                commandBuffer.ShaderObjectExt.CmdSetScissorWithCount(
                    commandBuffer.Underlying,
                    value
                );
        }

        // --- Rasterization ---
        public CullModeFlags CullMode
        {
            set => commandBuffer.ShaderObjectExt.CmdSetCullMode(commandBuffer.Underlying, value);
        }

        public FrontFace FrontFace
        {
            set => commandBuffer.ShaderObjectExt.CmdSetFrontFace(commandBuffer.Underlying, value);
        }

        public PolygonMode PolygonMode
        {
            set => commandBuffer.ShaderObjectExt.CmdSetPolygonMode(commandBuffer.Underlying, value);
        }

        public bool RasterizerDiscardEnable
        {
            set =>
                commandBuffer.ShaderObjectExt.CmdSetRasterizerDiscardEnable(
                    commandBuffer.Underlying,
                    value
                );
        }

        public bool DepthBiasEnable
        {
            set =>
                commandBuffer.ShaderObjectExt.CmdSetDepthBiasEnable(
                    commandBuffer.Underlying,
                    value
                );
        }

        public bool DepthClampEnable
        {
            set =>
                commandBuffer.ShaderObjectExt.CmdSetDepthClampEnable(
                    commandBuffer.Underlying,
                    value
                );
        }

        public void SetDepthClampRange(DepthClampModeEXT mode, in DepthClampRangeEXT range)
        {
            commandBuffer.ShaderObjectExt.CmdSetDepthClampRange(
                commandBuffer.Underlying,
                mode,
                in range
            );
        }

        public bool DepthClipEnable
        {
            set =>
                commandBuffer.ShaderObjectExt.CmdSetDepthClipEnable(
                    commandBuffer.Underlying,
                    value
                );
        }

        public bool DepthClipNegativeOneToOne
        {
            set =>
                commandBuffer.ShaderObjectExt.CmdSetDepthClipNegativeOneToOne(
                    commandBuffer.Underlying,
                    value
                );
        }

        public ProvokingVertexModeEXT ProvokingVertexMode
        {
            set =>
                commandBuffer.ShaderObjectExt.CmdSetProvokingVertexMode(
                    commandBuffer.Underlying,
                    value
                );
        }

        public uint RasterizationStream
        {
            set =>
                commandBuffer.ShaderObjectExt.CmdSetRasterizationStream(
                    commandBuffer.Underlying,
                    value
                );
        }

        public ConservativeRasterizationModeEXT ConservativeRasterizationMode
        {
            set =>
                commandBuffer.ShaderObjectExt.CmdSetConservativeRasterizationMode(
                    commandBuffer.Underlying,
                    value
                );
        }

        public float ExtraPrimitiveOverestimationSize
        {
            set =>
                commandBuffer.ShaderObjectExt.CmdSetExtraPrimitiveOverestimationSize(
                    commandBuffer.Underlying,
                    value
                );
        }

        // --- Line rasterization ---
        public LineRasterizationModeEXT LineRasterizationMode
        {
            set =>
                commandBuffer.ShaderObjectExt.CmdSetLineRasterizationMode(
                    commandBuffer.Underlying,
                    value
                );
        }

        public bool LineStippleEnable
        {
            set =>
                commandBuffer.ShaderObjectExt.CmdSetLineStippleEnable(
                    commandBuffer.Underlying,
                    value
                );
        }

        // --- Multisampling ---
        public SampleCountFlags RasterizationSamples
        {
            set =>
                commandBuffer.ShaderObjectExt.CmdSetRasterizationSamples(
                    commandBuffer.Underlying,
                    value
                );
        }

        public void SetSampleMask(SampleCountFlags samples, in uint sampleMask)
        {
            commandBuffer.ShaderObjectExt.CmdSetSampleMask(
                commandBuffer.Underlying,
                samples,
                in sampleMask
            );
        }

        public bool AlphaToCoverageEnable
        {
            set =>
                commandBuffer.ShaderObjectExt.CmdSetAlphaToCoverageEnable(
                    commandBuffer.Underlying,
                    value
                );
        }

        public bool AlphaToOneEnable
        {
            set =>
                commandBuffer.ShaderObjectExt.CmdSetAlphaToOneEnable(
                    commandBuffer.Underlying,
                    value
                );
        }

        public bool SampleLocationsEnable
        {
            set =>
                commandBuffer.ShaderObjectExt.CmdSetSampleLocationsEnable(
                    commandBuffer.Underlying,
                    value
                );
        }

        // --- Depth / Stencil ---
        public bool DepthTestEnable
        {
            set =>
                commandBuffer.ShaderObjectExt.CmdSetDepthTestEnable(
                    commandBuffer.Underlying,
                    value
                );
        }

        public bool DepthWriteEnable
        {
            set =>
                commandBuffer.ShaderObjectExt.CmdSetDepthWriteEnable(
                    commandBuffer.Underlying,
                    value
                );
        }

        public CompareOp DepthCompareOp
        {
            set =>
                commandBuffer.ShaderObjectExt.CmdSetDepthCompareOp(commandBuffer.Underlying, value);
        }

        public bool DepthBoundsTestEnable
        {
            set =>
                commandBuffer.ShaderObjectExt.CmdSetDepthBoundsTestEnable(
                    commandBuffer.Underlying,
                    value
                );
        }

        public bool StencilTestEnable
        {
            set =>
                commandBuffer.ShaderObjectExt.CmdSetStencilTestEnable(
                    commandBuffer.Underlying,
                    value
                );
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
                commandBuffer.Underlying,
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
                commandBuffer.Underlying,
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
                commandBuffer.Underlying,
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
                commandBuffer.Underlying,
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
                commandBuffer.Underlying,
                firstAttachment,
                blendAdvanced
            );
        }

        public bool LogicOpEnable
        {
            set =>
                commandBuffer.ShaderObjectExt.CmdSetLogicOpEnable(commandBuffer.Underlying, value);
        }

        public LogicOp LogicOp
        {
            set => commandBuffer.ShaderObjectExt.CmdSetLogicOp(commandBuffer.Underlying, value);
        }
    }
}
