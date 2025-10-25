using System.Diagnostics;

namespace GameDotNet.Core.Tooling;

/// <summary>
/// Reimplementation of Stopwatch as a struct that uses TimeProvider.
/// </summary>
/// <param name="provider">Time provider to get timestamps</param>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public struct ValueStopwatch(TimeProvider provider)
{
    public ValueStopwatch() : this(TimeProvider.System) { }

    private long _elapsed;
    private long _startTimeStamp;

    // performance-counter frequency, in counts per ticks.
    // This can speed up conversion to ticks.
    private readonly double _tickFrequency = (double)TimeSpan.TicksPerSecond / provider.TimestampFrequency;

    public void Start()
    {
        if (!IsRunning)
        {
            _startTimeStamp = provider.GetTimestamp();
            IsRunning = true;
        }
    }
    
    public static ValueStopwatch StartNew(TimeProvider provider)
    {
        var sw = new ValueStopwatch(provider);
        sw.Start();
        return sw;
    }
    
    public void Stop()
    {
        if (IsRunning)
        {
            _elapsed += provider.GetTimestamp() - _startTimeStamp;
            IsRunning = false;
        }
    }
    
    public void Reset()
    {
        _elapsed = 0;
        _startTimeStamp = 0;
        IsRunning = false;
    }
    
    public void Restart()
    {
        _elapsed = 0;
        _startTimeStamp = provider.GetTimestamp();
        IsRunning = true;
    }
    
            /// <summary>
        /// Returns the <see cref="Elapsed"/> time as a string.
        /// </summary>
        /// <returns>
        /// Elapsed time string in the same format used by <see cref="TimeSpan.ToString()"/>.
        /// </returns>
        public override string ToString() => Elapsed.ToString();

        public bool IsRunning { get; private set; }

        public TimeSpan Elapsed => new(ElapsedTimeSpanTicks);

        public long ElapsedMilliseconds => ElapsedTimeSpanTicks / TimeSpan.TicksPerMillisecond;

        public long ElapsedTicks
        {
            get
            {
                var timeElapsed = _elapsed;

                // If the Stopwatch is running, add elapsed time since the Stopwatch is started last time.
                if (IsRunning)
                {
                    timeElapsed += provider.GetTimestamp() - _startTimeStamp;
                }

                return timeElapsed;
            }
        }

        private long ElapsedTimeSpanTicks => (long)(ElapsedTicks * _tickFrequency);

        private string DebuggerDisplay => $"{Elapsed} (IsRunning = {IsRunning})";
}