using unsafe QuerySetPtr = Silk.NET.WebGPU.QuerySet*;

namespace GameDotNet.Graphics.WGPU.Wrappers;

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