using System.Runtime.InteropServices;
using GameDotNet.Graphics.Abstractions;
using Microsoft.Extensions.Logging;
using Silk.NET.SPIRV;
using Silk.NET.SPIRV.Reflect;
using Silk.NET.WebGPU;
using SpirvReflectSharp;
using BindGroupLayout = GameDotNet.Graphics.WGPU.Wrappers.BindGroupLayout;
using ShaderModule = GameDotNet.Graphics.WGPU.Wrappers.ShaderModule;
using ShaderStage = Silk.NET.WebGPU.ShaderStage;
using VertexBufferLayout = GameDotNet.Graphics.WGPU.Wrappers.VertexBufferLayout;
using VertexState = GameDotNet.Graphics.WGPU.Wrappers.VertexState;

namespace GameDotNet.Graphics.WGPU;

public sealed class WebGpuShader : IShader
{
    public ShaderDescription Description { get; }
    public ShaderModule? Module { get; private set; }


    private readonly WebGpuContext _ctx;
    private readonly SpirVShader _bytecode;
    private readonly ILogger _logger;
    private readonly SpirvReflectSharp.ShaderModule _reflectModule;


    public WebGpuShader(WebGpuContext ctx, SpirVShader bytecode, ILogger logger)
    {
        _logger = logger;
        _ctx = ctx;
        _bytecode = bytecode;
        Description = bytecode.Description;
        _reflectModule = SpirvReflect.ReflectCreateShaderModule(bytecode.Code);
    }

    public async ValueTask<ShaderModule?> Compile(CancellationToken token = default)
    {
        var tcs = new TaskCompletionSource();
        ShaderModule module;
        try
        {
            module = _ctx.Device.CreateSpirVShaderModule("create-shader-module-spirv", _bytecode.Code);
        }
        catch (SEHException e)
        {
            _logger.LogError("Shader module {Name} compilation failed, check uncaptured output for more details", _bytecode.Description.Name);
            return null;
        }
        
        token.ThrowIfCancellationRequested();
        module.GetCompilationInfo((status, messages) =>
        {
            token.ThrowIfCancellationRequested();
            if (status is not CompilationInfoRequestStatus.Success)
            {
                _logger.LogError("Shader module {Name} compilation failed : {Message}", _bytecode.Description.Name,string.Join(Environment.NewLine, messages.ToArray()));
                return;
            }
            
            tcs.SetResult();
        });

        await tcs.Task;
        Module = module;
        return module;
    }

    public VertexState GetVertexState()
    {
        var entryPoint = _reflectModule.EntryPoints.Single();

        var stride = 0ul;
        var attributes = _reflectModule.EnumerateInputVariables()
                                       .Select(v => new VertexAttribute(v.Format.ToVertexFormat(), 0, v.Location))
                                       .OrderBy(att => att.ShaderLocation)
                                       .Select(att1 =>
                                       {
                                           var formatSize = att1.Format.GetByteSize();
                                           var att2 = att1 with { Offset = stride };
                                           stride += formatSize;
                                           return att2;
                                       }).ToArray();

        var vertBufferLayout = new VertexBufferLayout
        {
            StepMode = VertexStepMode.Vertex,
            ArrayStride = 0,
            Attributes = attributes
        };

        return new()
        {
            Module = Module ??
                     throw new
                         InvalidOperationException("Cannot create vertex state when shader module is null. Was it compiled?"),
            EntryPoint = entryPoint.Name,
            BufferLayouts = new[] { vertBufferLayout }
        };
    }

    public BindGroupLayout[] GetPipelineGroupBindLayouts()
    {
        var entry = _reflectModule.EntryPoints.Single();

        var sets = entry.DescriptorSets;

        return sets.Select(set =>
        {
            var bindGroupLayoutEntries = set.Bindings.Select(binding =>
            {
                var bindEntry = new BindGroupLayoutEntry
                {
                    Binding = binding.Binding,
                    Visibility = entry.SpirvExecutionModel switch
                    {
                        ExecutionModel.Vertex => ShaderStage.Vertex,
                        ExecutionModel.Fragment => ShaderStage.Fragment,
                        ExecutionModel.GLCompute => ShaderStage.Compute,
                        _ => throw new ArgumentOutOfRangeException(nameof(entry.SpirvExecutionModel),
                                                                   "SpirV shader execution model is not supported by WebGPU")
                    }
                };

                

                if (binding.DescriptorType is DescriptorType.UniformBuffer)
                {
                    bindEntry.Buffer.Type = BufferBindingType.Uniform;
                }

                if (binding.DescriptorType is DescriptorType.StorageBuffer)
                {
                    bindEntry.Buffer.Type = BufferBindingType.Storage;
                }
                

                return bindEntry;
            }).ToArray();

            return _ctx.Device.CreateBindgroupLayout("bind-group-layout-create", bindGroupLayoutEntries);
        }).ToArray();
    }

    private ulong MinByteSizeFromTypeDef(ReflectTypeDescription type)
    {
        if (type.TypeFlags.HasFlag(TypeFlagBits.Vector))
        {
            var count = type.Traits.Numeric.Vector.ComponentCount;
            
        }

        return 0;
    }
    
    public void Dispose()
    {
        Module?.Dispose();
        _reflectModule.Dispose();
    }
}