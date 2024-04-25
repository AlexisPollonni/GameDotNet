using Serilog;
using Serilog.Sinks.Async;
using Timer = System.Timers.Timer;

namespace GameDotNet.Hosting;

internal sealed class AsyncSinkMonitorHook : IAsyncLogEventSinkMonitor
{
    public Func<ILogger?>? SelfLogFactory { get; set; }

    private readonly object _lock;

    private Timer? _timer;
    private IAsyncLogEventSinkInspector? _inspector;
    private long _lastDroppedCount;

    public AsyncSinkMonitorHook()
    {
        _lastDroppedCount = 0;
        _lock = new();
    }

    public void StartMonitoring(IAsyncLogEventSinkInspector inspector)
    {
        _inspector = inspector;
        if (_timer is not null) return;

        _timer = new();
        _timer.Interval = 1000;
        _timer.Elapsed += Timer_Elapsed;
        _timer.Start();
    }

    private void Timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (_inspector is null) return;

        var usagePct = (float)_inspector.Count / _inspector.BufferSize;
        if (usagePct <= 0.8) return;

        long dropped;
        int queueCount;
        lock (_lock)
        {
            queueCount = _inspector.Count;
            dropped = _inspector.DroppedMessagesCount - _lastDroppedCount;
            _lastDroppedCount = _inspector.DroppedMessagesCount;
        }

        if (dropped > 0)
            SelfLogFactory?.Invoke()?.Warning("Log buffer overflow: dropped {DropCount} messages on {QueuedCount}/{BufferSize} ({PercentFill}%)",
                             dropped, queueCount, _inspector.BufferSize, (float)queueCount / _inspector.BufferSize);
    }

    public void StopMonitoring(IAsyncLogEventSinkInspector inspector)
    {
        if (_timer is null) return;
        _timer.Stop();
        _timer.Dispose();
        _timer = null;
    }
}