using System.Runtime.InteropServices;
using System.Threading.Tasks.Sources;
using Microsoft.Extensions.ObjectPool;

namespace GameDotNet.Core.Tooling;

public sealed class PooledValueTaskSource : IValueTaskSource, IResettable
{
    public short Version => _core.Version;

    private ManualResetValueTaskSourceCore<VoidTaskResult> _core;

    public bool RunContinuationAsynchronously
    {
        get => _core.RunContinuationsAsynchronously;
        set => _core.RunContinuationsAsynchronously = value;
    }

    void IValueTaskSource.GetResult(short token)
    {
        _core.GetResult(token);
    }

    ValueTaskSourceStatus IValueTaskSource.GetStatus(short token) => _core.GetStatus(token);

    void IValueTaskSource.OnCompleted(
        Action<object?> continuation,
        object? state,
        short token,
        ValueTaskSourceOnCompletedFlags flags
    )
    {
        _core.OnCompleted(continuation, state, token, flags);
    }

    public ValueTask AsValueTask()
    {
        return new(this, _core.Version);
    }

    public bool TryReset()
    {
        try
        {
            _core.Reset();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void SetResult()
    {
        _core.SetResult(new());
    }

    public void SetException(Exception exception)
    {
        _core.SetException(exception);
    }

    [StructLayout(LayoutKind.Sequential, Size = 1)]
    private readonly struct VoidTaskResult;
}
