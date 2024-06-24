using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;
using GameDotNet.Graphics.Abstractions;
using GameDotNet.Graphics.WGPU.Extensions;
using Microsoft.Extensions.Logging;
using Silk.NET.WebGPU;
using SpirvReflectSharp;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using ShaderModule = GameDotNet.Graphics.WGPU.Wrappers.ShaderModule;
using VertexBufferLayout = GameDotNet.Graphics.WGPU.Wrappers.VertexBufferLayout;
using VertexState = GameDotNet.Graphics.WGPU.Wrappers.VertexState;

namespace GameDotNet.Graphics.WGPU;

public sealed partial class WebGpuShader : IShader
{
    public ShaderDescription Description => Source.Description;
    public ShaderModule? Module { get; private set; }
    public SpirVShader Source { get; }


    private readonly WebGpuContext _ctx;
    private readonly ILogger _logger;
    private readonly SpirvReflectSharp.ShaderModule _reflectModule;


    public WebGpuShader(WebGpuContext ctx, SpirVShader source, ILogger logger)
    {
        _logger = logger;
        _ctx = ctx;
        Source = source;
        
        _reflectModule = SpirvReflect.ReflectCreateShaderModule(source.Code.AsMemory().AsBytes().Span);
    }

    public async ValueTask<ShaderModule?> Compile(CancellationToken token = default)
    {
        var tcs = new TaskCompletionSource();
        ShaderModule module;
        try
        {
            module = _ctx.Device.CreateSpirVShaderModule($"create-shader-module-spirv:{Source.Description.Stage}/{Source.Description.Name}", Source.Code, Source.Description.EntryPoint);
        }
        catch (SEHException e)
        {
            WebGpuCompileFailed(e, Source.Description.Name);
            return null;
        }
        
        token.ThrowIfCancellationRequested();

        //TODO: Add when compilation info is implemented in WGPU (maybe 2.22 silk.net)
        // module.GetCompilationInfo((status, messages) =>
        // {
        //     token.ThrowIfCancellationRequested();
        //     if (status is not CompilationInfoRequestStatus.Success)
        //     {
        //         _logger.LogError("Shader module {Name} compilation failed : {Message}", Source.Description.Name,string.Join(Environment.NewLine, messages.ToArray()));
        //         return;
        //     }
        //     
        //     tcs.SetResult();
        // });
        tcs.SetResult();
        
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
            ArrayStride = stride,
            Attributes = attributes
        };

        return new()
        {
            Module = Module ??
                     throw new
                         InvalidOperationException("Cannot create vertex state when shader module is null. Was it compiled?"),
            EntryPoint = entryPoint.Name,
            BufferLayouts = [vertBufferLayout]
        };
    }
    
    public void Dispose()
    {
        Module?.Dispose();
        _reflectModule.Dispose();
    }

    [LoggerMessage(LogLevel.Error,
        Message = "Shader module {Name} compilation failed, check un-captured output for more details")]
    private partial void WebGpuCompileFailed(Exception ex, string name);
}