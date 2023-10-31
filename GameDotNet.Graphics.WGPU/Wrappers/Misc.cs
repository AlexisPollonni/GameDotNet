using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;

using unsafe ComputePipelinePtr = Silk.NET.WebGPU.ComputePipeline*;
using unsafe PipelineLayoutPtr = Silk.NET.WebGPU.PipelineLayout*;
using unsafe QuerySetPtr = Silk.NET.WebGPU.QuerySet*;
using unsafe RenderPipelinePtr = Silk.NET.WebGPU.RenderPipeline*;
using unsafe SamplerPtr = Silk.NET.WebGPU.Sampler*;
using unsafe ShaderModulePtr = Silk.NET.WebGPU.ShaderModule*;
using unsafe SurfacePtr = Silk.NET.WebGPU.Surface*;
using unsafe CommandBufferPtr = Silk.NET.WebGPU.CommandBuffer*;
using unsafe RenderBundlePtr = Silk.NET.WebGPU.RenderBundle*;


namespace GameDotNet.Graphics.WGPU.Wrappers;

public sealed class ComputePipeline : IDisposable
{
    private readonly WebGPU _api;
    private unsafe ComputePipelinePtr _handle;

    internal unsafe ComputePipelinePtr Handle
    {
        get
        {
            if (_handle is null)
                throw new HandleDroppedOrDestroyedException(nameof(ComputePipeline));

            return _handle;
        }

        private set => _handle = value;
    }

    internal unsafe ComputePipeline(WebGPU api, ComputePipelinePtr handle)
    {
        if (handle is null)
            throw new ResourceCreationError(nameof(ComputePipeline));

        _api = api;
        _handle = handle;
    }

    public unsafe BindGroupLayout GetBindGroupLayout(uint groupIndex)
        => new(_api, _api.ComputePipelineGetBindGroupLayout(_handle, groupIndex));

    public unsafe void SetLabel(string label) => _api.ComputePipelineSetLabel(_handle, label);
        
    public unsafe void Dispose()
    {
        _api.ComputePipelineRelease(_handle);
        _handle = null;
    }
}

public sealed class PipelineLayout : IDisposable
{
    private readonly WebGPU _api;
    private unsafe PipelineLayoutPtr _handle;

    internal unsafe PipelineLayoutPtr Handle 
    {
        get
        {
            if (_handle is null)
                throw new HandleDroppedOrDestroyedException(nameof(PipelineLayout));

            return _handle;
        }

        private set => _handle = value;
    }

    internal unsafe PipelineLayout(WebGPU api, PipelineLayoutPtr handle)
    {
        if (handle is null)
            throw new ResourceCreationError(nameof(PipelineLayout));

        _api = api;
        _handle = handle;
    }
        
    public unsafe void Dispose()
    {
        _api.PipelineLayoutRelease(_handle);
        _handle = null;
    }
}

public sealed class QuerySet : IDisposable
{
    private readonly WebGPU _api;
    private unsafe QuerySetPtr _handle;

    internal unsafe QuerySet(WebGPU api, QuerySetPtr handle)
    {
        if (handle is null)
            throw new ResourceCreationError(nameof(QuerySet));
        _api = api;

        _handle = handle;
    }

    internal unsafe QuerySetPtr Handle
    {
        get
        {
            if (_handle is null)
                throw new HandleDroppedOrDestroyedException(nameof(QuerySet));

            return _handle;
        }

        private set => _handle = value;
    }
        
    public unsafe void Dispose()
    {
        _api.QuerySetDestroy(_handle);
        _api.QuerySetRelease(_handle);
        _handle = null;
    }
}

public sealed class RenderPipeline : IDisposable
{
    private readonly WebGPU _api;
    private unsafe RenderPipelinePtr _handle;

    internal unsafe RenderPipelinePtr Handle 
    {
        get
        {
            if (_handle is null)
                throw new HandleDroppedOrDestroyedException(nameof(RenderPipeline));

            return _handle;
        }

        private set => _handle = value;
    }

    internal unsafe RenderPipeline(WebGPU api, RenderPipelinePtr handle)
    {
        if (handle is null)
            throw new ResourceCreationError(nameof(RenderPipeline));
        _api = api;

        Handle = handle;
    }

    public unsafe BindGroupLayout GetBindGroupLayout(uint groupIndex)
        => new(_api, _api.RenderPipelineGetBindGroupLayout(_handle, groupIndex));

    public unsafe void SetLabel(string label) => _api.RenderPipelineSetLabel(_handle, label);
        
    public unsafe void Dispose()
    {
        _api.RenderPipelineRelease(_handle);
        Handle = null;
    }
}

public sealed class Sampler : IDisposable
{
    private readonly WebGPU _api;
    private unsafe SamplerPtr _handle;

    internal unsafe SamplerPtr Handle 
    {
        get
        {
            if (_handle is null)
                throw new HandleDroppedOrDestroyedException(nameof(Sampler));

            return _handle;
        }

        private set => _handle = value;
    }

    internal unsafe Sampler(WebGPU api, SamplerPtr handle)
    {
        if (handle is null)
            throw new ResourceCreationError(nameof(Sampler));
        _api = api;

        Handle = handle;
    }
        
    public unsafe void Dispose()
    {
        _api.SamplerRelease(_handle);
        _handle = null;
    }
}

public sealed class ShaderModule : IDisposable
{
    private readonly WebGPU _api;
    private unsafe ShaderModulePtr _handle;

    internal unsafe ShaderModulePtr Handle 
    {
        get
        {
            if (_handle is null)
                throw new HandleDroppedOrDestroyedException(nameof(ShaderModule));

            return _handle;
        }

        private set => _handle = value;
    }

    internal unsafe ShaderModule(WebGPU api, ShaderModulePtr handle)
    {
        if (handle is null)
            throw new ResourceCreationError(nameof(ShaderModule));
        _api = api;

        Handle = handle;
    }

    public unsafe void GetCompilationInfo(CompilationInfoCallback callback)
    {
        _api.ShaderModuleGetCompilationInfo(_handle,
                                            new ((s, c, _) =>
                                            {
                                                callback(s,
                                                         new(c->Messages, (int)c->MessageCount)
                                                        );

                                            }), null);
    }

    public unsafe void SetLabel(string label) => _api.ShaderModuleSetLabel(_handle, label);
        
    public unsafe void Dispose()
    {
        _api.ShaderModuleRelease(_handle);
        _handle = null;
    }
}

public delegate void CompilationInfoCallback(CompilationInfoRequestStatus status,
                                             ReadOnlySpan<CompilationMessage> messages);

public delegate void QueueWorkDoneCallback(QueueWorkDoneStatus status); 

public sealed class CommandBuffer : IDisposable
{
    private readonly WebGPU _api;
    private unsafe CommandBufferPtr _handle;

    internal unsafe CommandBufferPtr Handle 
    {
        get
        {
            if (_handle is null)
                throw new HandleDroppedOrDestroyedException(nameof(CommandBuffer));

            return _handle;
        }

        private set => _handle = value;
    }

    internal unsafe CommandBuffer(WebGPU api, CommandBufferPtr handle)
    {
        if (handle is null)
            throw new ResourceCreationError(nameof(CommandBuffer));
        _api = api;

        Handle = handle;
    }
        
    public unsafe void Dispose()
    {
        _api.CommandBufferRelease(_handle);
        _handle = null;
    }
}

public sealed class RenderBundle : IDisposable
{
    private readonly WebGPU _api;
    private unsafe RenderBundlePtr _handle;

    internal unsafe RenderBundlePtr Handle 
    {
        get
        {
            if (_handle is null)
                throw new HandleDroppedOrDestroyedException(nameof(RenderBundle));

            return _handle;
        }

        private set => _handle = value;
    }

    internal unsafe RenderBundle(WebGPU api, RenderBundlePtr handle)
    {
        if (handle is null)
            throw new ResourceCreationError(nameof(RenderBundle));
        _api = api;
        Handle = handle;
    }
        
    public unsafe void Dispose()
    {
        _api.RenderBundleRelease(_handle);
        _handle = null;
    }
}