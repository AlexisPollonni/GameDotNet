using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.PlatformServices;

namespace GameDotNet.Hosting;

public sealed class MainThreadScheduler : LocalScheduler, ISchedulerPeriodic, IDisposable
{
    /// <summary>
    /// Indicates whether the event loop Run method is allowed to return when no work is left. If new work
    /// is scheduled afterwards, you will need to call the Run method loop again to process work.
    /// </summary>
    public bool ExitIfEmpty { get; set; } = false;

    private readonly IStopwatch _stopwatch;
    private readonly object _gate;
    private readonly SemaphoreSlim _evt;
    private readonly SchedulerQueue<TimeSpan> _queue;
    private readonly Queue<ScheduledItem<TimeSpan>> _readyList;
    private ScheduledItem<TimeSpan>? _nextItem;
    private readonly SerialDisposable _nextTimer;
    private bool _disposed;
    private readonly IConcurrencyAbstractionLayer _cal;

    public MainThreadScheduler()
    {
        _cal = PlatformEnlightenmentProvider.Current.GetService<IConcurrencyAbstractionLayer>()!;
        _stopwatch = _cal.StartStopwatch();

        _gate = new();
        _evt = new(0);
        _queue = new();
        _readyList = new();
        _nextTimer = new();
    }

    public override IDisposable Schedule<TState>(TState state,
                                                 TimeSpan dueTime,
                                                 Func<IScheduler, TState, IDisposable> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var due = _stopwatch.Elapsed + dueTime;
        var si = new ScheduledItem<TimeSpan, TState>(this, state, action, due);

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (dueTime <= TimeSpan.Zero)
            {
                _readyList.Enqueue(si);
                _evt.Release();
            }
            else
            {
                _queue.Enqueue(si);
                _evt.Release();
            }
        }

        return si;
    }

    public IDisposable SchedulePeriodic<TState>(TState state, TimeSpan period, Func<TState, TState> action)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(action);

        return new PeriodicallyScheduledWorkItem<TState>(this, state, period, action);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;

            _disposed = true;
            _nextTimer.Dispose();
            _evt.Release();
        }
    }

    /// <summary>
    /// Event loop scheduled on the designated event loop thread. The loop is suspended/resumed using the event
    /// which gets set by calls to Schedule, the next item timer, or calls to Dispose.
    /// </summary>
    internal void Run()
    {
        while (true)
        {
            _evt.Wait();

            ScheduledItem<TimeSpan>[]? ready = null;

            lock (_gate)
            {
                //
                // Bug fix that ensures the number of calls to Release never greatly exceeds the number of calls to Wait.
                // See work item #37: https://rx.codeplex.com/workitem/37
                //
                while (_evt.CurrentCount > 0)
                {
                    _evt.Wait();
                }

                //
                // The event could have been set by a call to Dispose. This takes priority over anything else. We quit the
                // loop immediately.
                //
                if (_disposed)
                {
                    _evt.Dispose();
                    return;
                }

                while (_queue.Count > 0 && _queue.Peek().DueTime <= _stopwatch.Elapsed)
                {
                    var item = _queue.Dequeue();
                    _readyList.Enqueue(item);
                }

                if (_queue.Count > 0)
                {
                    var next = _queue.Peek();
                    if (next != _nextItem)
                    {
                        _nextItem = next;

                        var due = next.DueTime - _stopwatch.Elapsed;
                        _nextTimer.Disposable = _cal.StartTimer(Tick, next, due);
                    }
                }

                if (_readyList.Count > 0)
                {
                    ready = _readyList.ToArray();
                    _readyList.Clear();
                }
            }

            if (ready != null)
            {
                foreach (var item in ready)
                {
                    if (item.IsCanceled) continue;

                    try
                    {
                        item.Invoke();
                    }
                    catch (ObjectDisposedException ex) when (ex.ObjectName == nameof(MainThreadScheduler))
                    {
                        // Since we are not inside the lock at this point
                        // the scheduler can be disposed before the item had a chance to run
                    }
                }
            }

            if (!ExitIfEmpty) continue;
            
            lock (_gate)
            {
                if (_readyList.Count == 0 && _queue.Count == 0) return;
            }
        }
    }

    private void Tick(object? state)
    {
        lock (_gate)
        {
            if (_disposed) return;
            var item = (ScheduledItem<TimeSpan>)state!;
            if (item == _nextItem)
            {
                _nextItem = null;
            }

            if (_queue.Remove(item))
            {
                _readyList.Enqueue(item);
            }

            _evt.Release();
        }
    }

    private sealed class PeriodicallyScheduledWorkItem<TState> : IDisposable
    {
        private readonly TimeSpan _period;
        private readonly Func<TState, TState> _action;
        private readonly MainThreadScheduler _scheduler;
        private readonly AsyncLock _gate = new();
        private readonly MultipleAssignmentDisposable _task;
        private readonly TState _state;

        private TimeSpan _next;

        public PeriodicallyScheduledWorkItem(MainThreadScheduler scheduler,
                                             TState state,
                                             TimeSpan period,
                                             Func<TState, TState> action)
        {
            _state = state;
            _period = period;
            _action = action;
            _scheduler = scheduler;
            _next = scheduler._stopwatch.Elapsed + period;
            _task = new();

            _task.Disposable = scheduler.Schedule(this, _next - scheduler._stopwatch.Elapsed, static (s, w) => w.Tick(s));
        }

        private IDisposable Tick(IScheduler self)
        {
            _next += _period;

            _task.Disposable = self.Schedule(this, _next - _scheduler._stopwatch.Elapsed, static (s, w) => w.Tick(s));

            _gate.Wait(() => _action(_state));

            return Disposable.Empty;
        }

        public void Dispose()
        {
            _task.Dispose();
            _gate.Dispose();
        }
    }
}