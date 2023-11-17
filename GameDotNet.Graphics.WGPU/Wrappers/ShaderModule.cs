using Silk.NET.WebGPU;
using unsafe ShaderModulePtr = Silk.NET.WebGPU.ShaderModule*;

namespace GameDotNet.Graphics.WGPU.Wrappers;

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