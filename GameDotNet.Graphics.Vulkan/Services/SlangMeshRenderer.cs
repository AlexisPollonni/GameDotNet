using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using GameDotNet.Graphics.Abstractions;
using GameDotNet.Graphics.Models;
using GameDotNet.Graphics.Tooling;
using GameDotNet.Graphics.Vulkan.Abstractions;
using GameDotNet.Graphics.Vulkan.MemoryAllocation;
using GameDotNet.Graphics.Vulkan.Tools.Extensions;
using GameDotNet.Graphics.Vulkan.Wrappers;
using Microsoft.Extensions.DependencyInjection;
using Silk.NET.Core;
using Silk.NET.Vulkan;
using SlangShaderSharp;

namespace GameDotNet.Graphics.Vulkan.Services;

/// <summary>
/// PoC mesh renderer: compiles a Slang shader, reflects vertex layout, and draws a
/// hard-coded triangle using dynamic rendering and the shader object extension.
/// </summary>
public sealed class SlangMeshRenderer : IEntityRenderer, IDisposable
{
    private readonly IVulkanContext _context;
    private readonly CommandSubmitter _submitter;
    private readonly TimeProvider _timeProvider;
    private readonly TimingsRingBuffer _timings = new(100);

    private readonly ShaderEXT _vertShader;
    private readonly ShaderEXT _fragShader;

    private readonly VulkanBuffer _vertexBuffer;
    private readonly VulkanBuffer _indexBuffer;
    private readonly uint _vertexCount;
    private readonly uint _indexCount;

    // Reflected from the Slang vertex shader
    private readonly VertexInputBindingDescription2EXT[] _vertexBindings;
    private readonly VertexInputAttributeDescription2EXT[] _vertexAttributes;

    private bool _disposed;

    private const string ShaderModuleName = "triangle";

    // Slang shader: vertex positions are in clip space (no MVP needed for PoC)
    private const string ShaderSource = """
        struct Interpolants {
            float4 position : SV_Position;
            [[vk::location(0)]] float4 color : COLOR0;
        }

        [shader("vertex")]
        Interpolants vertMain(
            [[vk::location(0)]] float3 position : POSITION,
            [[vk::location(1)]] float3 normal   : NORMAL,
            [[vk::location(2)]] float4 color     : COLOR0
        ) {
            Interpolants output;
            output.position = float4(position, 1.0);
            output.color    = color;
            return output;
        }

        [shader("fragment")]
        [[vk::location(0)]] float4 fragMain(Interpolants input) : SV_Target {
            return input.color;
        }
        """;

    public SlangMeshRenderer(
        IVulkanContext context,
        TimeProvider timeProvider,
        SlangShaderService shaderService,
        [FromKeyedServices(QueueFlags.GraphicsBit)] CommandSubmitter submitter
    )
    {
        _context = context;
        _timeProvider = timeProvider;
        _submitter = submitter;

        // Compile and link Slang shader
        var program = shaderService.Compile(ShaderModuleName, ShaderSource);

        _vertShader = context.Device.CreateShaderObject(
            "vertMain",
            program.SpirvPerStage[SlangStage.Vertex].Span,
            ShaderStageFlags.VertexBit,
            ShaderStageFlags.FragmentBit,
            [],
            []
        );

        _fragShader = context.Device.CreateShaderObject(
            "fragMain",
            program.SpirvPerStage[SlangStage.Fragment].Span,
            ShaderStageFlags.FragmentBit,
            ShaderStageFlags.None,
            [],
            []
        );

        // Build vertex input layout from Slang reflection
        (_vertexBindings, _vertexAttributes) = BuildVertexLayout(program.Reflection);

        // Hard-coded triangle in clip/NDC space
        Vertex[] vertices =
        [
            new Vertex(new Vector3(0.0f, -0.5f, 0.5f), Vector3.UnitZ, Color.Red),
            new Vertex(new Vector3(0.5f, 0.5f, 0.5f), Vector3.UnitZ, Color.Lime),
            new Vertex(new Vector3(-0.5f, 0.5f, 0.5f), Vector3.UnitZ, Color.Blue),
        ];
        uint[] indices = [0, 1, 2];

        _vertexCount = (uint)vertices.Length;
        _indexCount = (uint)indices.Length;

        (_vertexBuffer, _indexBuffer) = UploadMesh(vertices, indices);
    }

    // ── Reflection helpers ──────────────────────────────────────────────────

    private static (
        VertexInputBindingDescription2EXT[],
        VertexInputAttributeDescription2EXT[]
    ) BuildVertexLayout(ShaderReflection reflection)
    {
        var vertEp = EntryPointReflection.Null;
        for (uint i = 0; i < reflection.EntryPointCount; i++)
        {
            var ep = reflection.GetEntryPointByIndex(i);
            if (ep.Stage != SlangStage.Vertex)
                continue;
            vertEp = ep;
            break;
        }

        if (vertEp == EntryPointReflection.Null)
            throw new InvalidOperationException("No vertex entry point found in shader reflection");

        var stride = (uint)Unsafe.SizeOf<Vertex>();
        VertexInputBindingDescription2EXT[] bindings =
        [
            new()
            {
                SType = StructureType.VertexInputBindingDescription2Ext,
                Binding = 0,
                Stride = stride,
                InputRate = VertexInputRate.Vertex,
                Divisor = 1,
            },
        ];

        uint paramCount = vertEp.ParameterCount;
        var attributes = new VertexInputAttributeDescription2EXT[paramCount];
        for (uint i = 0; i < paramCount; i++)
        {
            var param = vertEp.GetParameterByIndex(i);
            uint location = (uint)param.GetOffset(SlangParameterCategory.VaryingInput);
            var format = SlangTypeToVkFormat(param.TypeLayout.Type);
            uint offset = SemanticToVertexOffset(param.SemanticName);

            attributes[i] = new()
            {
                SType = StructureType.VertexInputAttributeDescription2Ext,
                Location = location,
                Binding = 0,
                Format = format,
                Offset = offset,
            };
        }

        return (bindings, attributes);
    }

    private static Format SlangTypeToVkFormat(TypeReflection type) =>
        type.Kind switch
        {
            SlangTypeKind.Scalar when type.ScalarType == SlangScalarType.Float32 =>
                Format.R32Sfloat,
            SlangTypeKind.Vector => (type.ScalarType, type.ColumnCount) switch
            {
                (SlangScalarType.Float32, 1) => Format.R32Sfloat,
                (SlangScalarType.Float32, 2) => Format.R32G32Sfloat,
                (SlangScalarType.Float32, 3) => Format.R32G32B32Sfloat,
                (SlangScalarType.Float32, 4) => Format.R32G32B32A32Sfloat,
                _ => throw new NotSupportedException(
                    $"Unsupported vector type: {type.ScalarType} x {type.ColumnCount}"
                ),
            },
            _ => throw new NotSupportedException(
                $"Unsupported type kind for vertex attribute: {type.Kind}"
            ),
        };

    /// <summary>Returns the byte offset of a vertex attribute within <see cref="Vertex"/>.</summary>
    private static uint SemanticToVertexOffset(string semanticName) =>
        semanticName.ToUpperInvariant() switch
        {
            "POSITION" => 0,
            "NORMAL" => (uint)Unsafe.SizeOf<Vector3>(),
            "COLOR" => (uint)(Unsafe.SizeOf<Vector3>() * 2),
            _ => throw new NotSupportedException($"Unknown vertex semantic: '{semanticName}'"),
        };

    // ── Mesh upload ─────────────────────────────────────────────────────────

    private unsafe (VulkanBuffer vertex, VulkanBuffer index) UploadMesh(
        Vertex[] vertices,
        uint[] indices
    )
    {
        var vertSize = (ulong)vertices.Length * (ulong)Unsafe.SizeOf<Vertex>();
        var idxSize = (ulong)indices.Length * sizeof(uint);

        var cpuGpuAlloc = new AllocationCreateInfo(usage: MemoryUsage.CPU_To_GPU);

        var vertBufInfo = new BufferCreateInfo(
            size: vertSize,
            usage: BufferUsageFlags.VertexBufferBit
        );
        var idxBufInfo = new BufferCreateInfo(
            size: idxSize,
            usage: BufferUsageFlags.IndexBufferBit
        );

        var vertBuf = new VulkanBuffer(_context.Allocator, in vertBufInfo, in cpuGpuAlloc);
        var idxBuf = new VulkanBuffer(_context.Allocator, in idxBufInfo, in cpuGpuAlloc);

        using (var m = vertBuf.Map<Vertex>())
            if (m.TryGetSpan(out var span))
                vertices.AsSpan().CopyTo(span);

        using (var m = idxBuf.Map<uint>())
            if (m.TryGetSpan(out var span))
                indices.AsSpan().CopyTo(span);

        return (vertBuf, idxBuf);
    }

    // ── Render ──────────────────────────────────────────────────────────────

    public async ValueTask<TimelineStats> Render(IDeviceTexture renderTarget)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var startTimestamp = _timeProvider.GetTimestamp();

        var image = (VulkanImage)renderTarget;
        using var colorView = image.GetImageView(image.Format, ImageAspectFlags.ColorBit);

        var batch = _submitter.CreateBatch();
        // Unsafe command recording extracted to a separate non-async method
        // (async methods cannot contain unsafe code in C#)
        RecordDrawCommands(image, colorView, batch);

        await _submitter.SubmitAsync(batch);

        _timings.Add(_timeProvider.GetElapsedTime(startTimestamp));
        return _timings.ComputeStats();
    }

    private unsafe void RecordDrawCommands(
        VulkanImage image,
        VulkanImageView colorView,
        BatchId batch
    )
    {
        uint width = image.Extent.Width;
        uint height = image.Extent.Height;

        using var recorder = _submitter.CreateRecorder(batch);
        var cmd = recorder.Buffer;

        cmd.TransitionLayout(
            image,
            ImageLayout.ColorAttachmentOptimal,
            AccessFlags.ColorAttachmentWriteBit
        );

        var clearValue = new ClearValue(color: new ClearColorValue(0f, 0f, 0f, 1f));
        var colorAttachment = new RenderingAttachmentInfo
        {
            SType = StructureType.RenderingAttachmentInfo,
            ImageView = colorView.ImageView,
            ImageLayout = ImageLayout.ColorAttachmentOptimal,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            ClearValue = clearValue,
        };

        var renderArea = new Rect2D(extent: new Extent2D(width, height));
        var renderInfo = new RenderingInfo
        {
            SType = StructureType.RenderingInfo,
            RenderArea = renderArea,
            LayerCount = 1,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorAttachment,
        };
        _context.Api.CmdBeginRendering(cmd, in renderInfo);

        // Bind vertex + fragment shaders
        cmd.BindShaders(
            [ShaderStageFlags.VertexBit, ShaderStageFlags.FragmentBit],
            [_vertShader, _fragShader]
        );

        // Vertex input from reflection
        cmd.SetVertexInput(_vertexBindings, _vertexAttributes);

        // Viewport / scissor
        cmd.Viewports = [new Viewport(0, 0, width, height, 0, 1)];
        cmd.Scissors = [renderArea];

        // Input assembly
        cmd.PrimitiveTopology = PrimitiveTopology.TriangleList;
        cmd.PrimitiveRestartEnable = false;

        // Rasterization
        cmd.RasterizerDiscardEnable = false;
        cmd.CullMode = CullModeFlags.None;
        cmd.FrontFace = FrontFace.CounterClockwise;
        cmd.PolygonMode = PolygonMode.Fill;
        cmd.DepthBiasEnable = false;
        cmd.DepthClampEnable = false;

        // Multisample
        cmd.RasterizationSamples = SampleCountFlags.Count1Bit;
        uint sampleMask = ~0u;
        cmd.SetSampleMask(SampleCountFlags.Count1Bit, in sampleMask);
        cmd.AlphaToCoverageEnable = false;
        cmd.AlphaToOneEnable = false;

        // Depth / stencil (all disabled)
        cmd.DepthTestEnable = false;
        cmd.DepthWriteEnable = false;
        cmd.DepthCompareOp = CompareOp.LessOrEqual;
        cmd.DepthBoundsTestEnable = false;
        cmd.StencilTestEnable = false;

        // Color blend (disabled)
        cmd.LogicOpEnable = false;
        cmd.SetColorBlendEnable(0, [(Bool32)false]);
        cmd.SetColorWriteMask(
            0,
            [
                ColorComponentFlags.RBit
                    | ColorComponentFlags.GBit
                    | ColorComponentFlags.BBit
                    | ColorComponentFlags.ABit,
            ]
        );

        // Bind vertex and index buffers
        var vkVertBuf = _vertexBuffer.Buffer;
        var vkIdxBuf = _indexBuffer.Buffer;
        ulong zero = 0;
        ulong vertBytes = _vertexCount * (ulong)Unsafe.SizeOf<Vertex>();
        ulong vertStride = (ulong)Unsafe.SizeOf<Vertex>();
        cmd.BindVertexBuffers2(0, [vkVertBuf], [zero], [vertBytes], [vertStride]);
        _context.Api.CmdBindIndexBuffer(cmd, vkIdxBuf, 0, IndexType.Uint32);

        _context.Api.CmdDrawIndexed(cmd, _indexCount, 1, 0, 0, 0);

        _context.Api.CmdEndRendering(cmd);

        cmd.TransitionLayout(image, ImageLayout.ShaderReadOnlyOptimal, AccessFlags.ShaderReadBit);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _vertexBuffer.Dispose();
        _indexBuffer.Dispose();
        _context.Device.DestroyShaderObject(_vertShader);
        _context.Device.DestroyShaderObject(_fragShader);
    }
}
