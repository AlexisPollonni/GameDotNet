using System.Collections.Frozen;
using GameDotNet.Core.Tools.Extensions;
using Silk.NET.WebGPU;
using BindGroup = GameDotNet.Graphics.WGPU.Wrappers.BindGroup;
using BindGroupEntry = GameDotNet.Graphics.WGPU.Wrappers.BindGroupEntry;
using BindGroupLayout = GameDotNet.Graphics.WGPU.Wrappers.BindGroupLayout;
using Buffer = GameDotNet.Graphics.WGPU.Wrappers.Buffer;
using Device = GameDotNet.Graphics.WGPU.Wrappers.Device;
using PipelineLayout = GameDotNet.Graphics.WGPU.Wrappers.PipelineLayout;
using RenderPassEncoder = GameDotNet.Graphics.WGPU.Wrappers.RenderPassEncoder;

namespace GameDotNet.Graphics.WGPU;

public sealed class ShaderParameters : IDisposable
{
    private record GroupData(
        (string Name, uint Offset)[] BindingOffsets,
        BindGroupLayout? Layout = null,
        BindGroup? Bind = null)
    {
        public BindGroupLayout? Layout { get; set; } = Layout;
        public BindGroup? Bind { get; set; } = Bind;
    }

    public IReadOnlyDictionary<string, ShaderEntry> ShaderEntries => _entries;
    public IDictionary<string, Buffer> UniformBuffers { get; }

    public WebGpuShader VertexStage { get; }
    public WebGpuShader? FragmentStage { get; }


    private readonly Device _device;
    private readonly FrozenDictionary<string, ShaderEntry> _entries;

    private FrozenDictionary<uint, GroupData>? _bindData;

    public ShaderParameters(WebGpuContext ctx, WebGpuShader vertexStage, WebGpuShader? fragmentStage = null)
    {
        if (!ctx.IsInitialized) throw new InvalidOperationException();

        _device = ctx.Device;
        UniformBuffers = new Dictionary<string, Buffer>();
        VertexStage = vertexStage;
        FragmentStage = fragmentStage;

        _entries = BuildEntries().ToFrozenDictionary(e => e.Name);
    }

    public void BuildLayouts()
    {
        if (_bindData != null)
        {
            foreach (var groupData in _bindData)
            {
                groupData.Value.Bind?.Dispose();
                groupData.Value.Layout?.Dispose();
            }

            _bindData = null;
        }

        _bindData ??= _entries.Values.GroupBy(entry => entry.Set)
            .Select(entries => KeyValuePair.Create(entries.Key,
                new GroupData(entries
                    .Where(e => e is UniformEntry
                    {
                        IsDynamic: true
                    })
                    .Select(e => (e.Name, 0u))
                    .ToArray()
                ))).ToFrozenDictionary();

        foreach (var (set, layout) in CreatePipelineGroupLayouts(_entries.Values))
        {
            _bindData[set].Layout = layout;
        }
    }

    public void BuildGroups()
    {
        var groups = _entries.Values.GroupBy(e => e.Set);

        foreach (var group in groups)
        {
            var entries = group.Select(entry =>
            {
                if (entry is not UniformEntry uniformEntry) throw new NotImplementedException();

                if (!UniformBuffers.TryGetValue(entry.Name, out var buffer))
                {
                    buffer = _device.CreateBuffer("create-default-buffer", false, uniformEntry.MinSize,
                        BufferUsage.Uniform | BufferUsage.CopyDst);
                    UniformBuffers[entry.Name] = buffer;
                }

                return new BindGroupEntry
                {
                    Binding = entry.Binding,
                    Offset = 0,
                    Buffer = buffer,
                    Size = uniformEntry.MinSize
                };
            }).ToArray();

            var bindData = _bindData[group.Key];
            var bindGroup = _device.CreateBindGroup("create-bind-group", bindData.Layout, entries);
            
            bindData.Bind?.Dispose();
            bindData.Bind = bindGroup;
        }
    }

    public void DynamicEntryAddOffset(string name, uint deltaOffset)
    {
        var set = _entries[name].Set;
        var offsets = _bindData[set].BindingOffsets;

        var binding = -1;
        for (var i = 0; i < offsets.Length; i++)
        {
            if (offsets[i].Name != name) continue;

            binding = i;
            break;
        }

        ref var current = ref offsets[binding].Offset;

        if (current + deltaOffset >= UniformBuffers[name].Size.GetBytes())
            throw new ArgumentOutOfRangeException(nameof(deltaOffset));

        current += deltaOffset;
    }

    public PipelineLayout CreatePipelineLayout()
    {
        return _device.CreatePipelineLayout("render-pipeline-layout",
            _bindData.Values.Select(data => data.Layout).ToArray());
    }

    public void DynamicEntryResetOffset(string name)
    {
        var set = _entries[name].Set;
        var offsets = _bindData[set].BindingOffsets;

        var binding = Array.FindIndex(offsets, tuple => tuple.Name == name);
        offsets[binding].Offset = 0;
    }

    public void BindStaticDescriptors(RenderPassEncoder e)
    {
        foreach (var (set, data) in _bindData)
        {
            if (data.BindingOffsets.Length is not 0)
                continue;

            e.SetBindGroup(set, data.Bind);
        }
    }

    public void BindDynamicDescriptors(RenderPassEncoder e)
    {
        foreach (var (set, data) in _bindData)
        {
            if (data.BindingOffsets.Length is 0)
                continue;

            // using a separate method to avoid temp memory overflow from stackalloc
            SetBindingOffsets(e, set, data);
        }
    }

    private static void SetBindingOffsets(RenderPassEncoder e, uint set, GroupData data)
    {
        Span<uint> offsets = stackalloc uint[data.BindingOffsets.Length];
        for (var i = 0; i < data.BindingOffsets.Length; i++)
        {
            offsets[i] = data.BindingOffsets[i].Offset;
        }

        e.SetBindGroup(set, data.Bind, offsets);
    }

    private IEnumerable<ShaderEntry> BuildEntries()
    {
        var vertexUniforms = VertexStage.Source.GetUniforms();
        var fragmentUniforms = FragmentStage?.Source.GetUniforms();

        return vertexUniforms.Concat(fragmentUniforms ?? Enumerable.Empty<ShaderEntry>());
    }


    private Dictionary<uint, BindGroupLayout> CreatePipelineGroupLayouts(IEnumerable<ShaderEntry> entries)
    {
        return entries
            .GroupBy(entry => entry.Set)
            .Select(group =>
            {
                var groupEntries = group.Select(entry =>
                {
                    return new BindGroupLayoutEntry
                    {
                        Binding = entry.Binding,
                        Visibility = entry.Stage switch
                        {
                            Abstractions.ShaderStage.Vertex => ShaderStage.Vertex,
                            Abstractions.ShaderStage.Fragment => ShaderStage.Fragment,
                            Abstractions.ShaderStage.Compute => ShaderStage.Compute,
                            _ => throw new ArgumentOutOfRangeException()
                        },
                        Buffer = GetBuffer(entry)
                    };
                }).ToArray();

                return (group.Key, _device!.CreateBindgroupLayout("bind-group-layout-create", groupEntries));
            }).ToDictionary(tuple => tuple.Key, tuple => tuple.Item2);

        BufferBindingLayout GetBuffer(ShaderEntry entry)
        {
            if (entry is UniformEntry ubo)
                return new()
                {
                    Type = BufferBindingType.Uniform,
                    MinBindingSize = ubo.MinSize,
                    HasDynamicOffset = ubo.IsDynamic
                };
            return new();
        }
    }


    public void Dispose()
    {
        if (_bindData is null) return;
        foreach (var (set, (_, layout, bindGroup)) in _bindData)
        {
            bindGroup?.Dispose();
            layout?.Dispose();
        }
    }
}