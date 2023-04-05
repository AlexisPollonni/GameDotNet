using GameDotNet.Core.Tools.Containers;
using GameDotNet.Core.Tools.Extensions;
using Silk.NET.Vulkan;

namespace GameDotNet.Core.Graphics.Vulkan.Bootstrap;

public class PipelineBuilder
{
    private readonly VulkanDevice _device;
    private readonly VulkanInstance _instance;

    public class PipelineOptions
    {
        public VertexInputDescription VertexInputDescription { get; set; }
        public IList<VulkanShader> ShaderStages { get; set; } = new List<VulkanShader>();
        public RenderPass RenderPass { get; set; }
        public PrimitiveTopology Topology { get; set; } = PrimitiveTopology.TriangleList;
        public PolygonMode PolygonMode { get; set; } = PolygonMode.Fill;
        public Viewport Viewport { get; set; }
        public Rect2D Scissor { get; set; }

        public bool EnableDepthTest { get; set; }
        public bool EnableDepthWrite { get; set; }
        public CompareOp DepthStencilCompare { get; set; }
    }

    public PipelineBuilder(VulkanInstance instance, VulkanDevice device)
    {
        _instance = instance;
        _device = device;
    }

    public unsafe VulkanPipeline Build(PipelineOptions options)
    {
        using var d = new DisposableList();

        var shaderStages = options.ShaderStages.Select(shader => shader.GetPipelineShaderInfo()).ToArray();
        var vertexCreateInfo = CreateVertexInputStageInfo(options.VertexInputDescription);
        var inputAssembly = CreateInputAssemblyStateInfo(options.Topology);
        var rasterizer = CreateRasterizationStateInfo(options.PolygonMode);
        var multisampling = CreateMultisampleStateInfo();

        var layoutInfo = CreatePipelineLayoutInfo(options.ShaderStages, d);

        _instance.Vk.CreatePipelineLayout(_device, layoutInfo, null, out var layout)
                 .ThrowOnError("Failed to create graphics pipeline layout");

        var viewportState = new PipelineViewportStateCreateInfo(viewportCount: 1,
                                                                pViewports: options.Viewport.AsPtr(d),
                                                                scissorCount: 1,
                                                                pScissors: options.Scissor.AsPtr(d));

        var colorBlendAttachment = CreateColorBlendAttachmentState();

        //dummy color blending
        var colorBlending = new PipelineColorBlendStateCreateInfo(logicOpEnable: false, logicOp: LogicOp.Copy,
                                                                  attachmentCount: 1,
                                                                  pAttachments: &colorBlendAttachment);

        var depthStencil =
            CreateDepthStencilStateInfo(options.EnableDepthTest, options.EnableDepthWrite, options.DepthStencilCompare);

        var pipelineInfo = new GraphicsPipelineCreateInfo(stageCount: (uint)shaderStages.Length,
                                                          pStages: shaderStages.AsPtr(d),
                                                          pVertexInputState: vertexCreateInfo.AsPtr(d),
                                                          pInputAssemblyState: inputAssembly.AsPtr(d),
                                                          pViewportState: &viewportState,
                                                          pRasterizationState: rasterizer.AsPtr(d),
                                                          pMultisampleState: multisampling.AsPtr(d),
                                                          pColorBlendState: &colorBlending,
                                                          layout: layout,
                                                          renderPass: options.RenderPass,
                                                          subpass: 0,
                                                          basePipelineHandle: null,
                                                          pDepthStencilState: &depthStencil);

        _instance.Vk.CreateGraphicsPipelines(_device, new(), 1, &pipelineInfo, null, out var pipeline)
                 .ThrowOnError("Couldn't create graphics pipeline");

        return new(_instance.Vk, _device, pipeline, layout);
    }


    private static unsafe PipelineLayoutCreateInfo CreatePipelineLayoutInfo(
        IEnumerable<VulkanShader> stages, ICompositeDisposable d)
    {
        var ranges = stages.SelectMany(shader => shader.GetPushConstantRanges()).ToList();

        return new(flags: 0, setLayoutCount: 0, pSetLayouts: null, pushConstantRangeCount: (uint)ranges.Count,
                   pPushConstantRanges: ranges.AsPtr(d));
    }

    private static unsafe PipelineVertexInputStateCreateInfo CreateVertexInputStageInfo(VertexInputDescription d)
    {
        return new(flags: d.Flags,
                   vertexBindingDescriptionCount: (uint)d.Bindings.Count,
                   pVertexBindingDescriptions: d.Bindings.Count is not 0 ? d.Bindings.AsPtr() : null,
                   vertexAttributeDescriptionCount: (uint)d.Attributes.Count,
                   pVertexAttributeDescriptions: d.Attributes.Count is not 0 ? d.Attributes.AsPtr() : null);
    }

    private static unsafe PipelineInputAssemblyStateCreateInfo
        CreateInputAssemblyStateInfo(PrimitiveTopology topology) =>
        new(topology: topology, primitiveRestartEnable: false);

    private static unsafe PipelineRasterizationStateCreateInfo CreateRasterizationStateInfo(PolygonMode polygonMode) =>
        new(depthClampEnable: false,
            rasterizerDiscardEnable: false, //discards all primitives before the rasterization stage if enabled which we don't want
            polygonMode: polygonMode,
            lineWidth: 1.0f,
            cullMode: CullModeFlags.None, //no backface cull
            frontFace: FrontFace.Clockwise,
            depthBiasEnable: false,
            depthBiasConstantFactor: 0,
            depthBiasClamp: 0,
            depthBiasSlopeFactor: 0);

    private static unsafe PipelineMultisampleStateCreateInfo CreateMultisampleStateInfo() =>
        new(sampleShadingEnable: false,
            rasterizationSamples: SampleCountFlags
                .Count1Bit, //multisampling defaulted to no multisampling (1 sample per pixel)
            minSampleShading: 1f,
            alphaToCoverageEnable: false,
            alphaToOneEnable: false);

    private static PipelineColorBlendAttachmentState CreateColorBlendAttachmentState() =>
        new(colorWriteMask: ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit,
            blendEnable: false);

    private static unsafe PipelineDepthStencilStateCreateInfo CreateDepthStencilStateInfo(
        bool depthTest, bool depthWrite, CompareOp compareOp) =>
        new(depthTestEnable: depthTest,
            depthWriteEnable: depthWrite,
            depthCompareOp: depthTest ? compareOp : CompareOp.Always,
            depthBoundsTestEnable: false,
            minDepthBounds: 0f,
            maxDepthBounds: 1f,
            stencilTestEnable: false);
}