using System.Collections.Concurrent;

namespace GameDotNet.Graphics;

public class TimingsRingBuffer
{
    private readonly ConcurrentQueue<TimeSpan> _timingsQueue;
    private readonly int _maximumCapacity;

    public TimingsRingBuffer(int maximumCapacity)
    {
        if(maximumCapacity <= 0)
            throw new ArgumentException("Capacity must be positive.");
        
        _maximumCapacity = maximumCapacity;
        _timingsQueue = new();
    }

    public void Add(TimeSpan time)
    {
        while (_timingsQueue.Count >= _maximumCapacity)
        {
            _timingsQueue.TryDequeue(out _);
        }

        _timingsQueue.Enqueue(time);
    }
    
    public TimelineStats ComputeStats()
    {
        TimelineStats stats = default;
        if (_timingsQueue.Count is 0) return stats;
        
        stats.Total = TimeSpan.Zero;
        var sumOfSquares = TimeSpan.Zero;
        stats.Min = TimeSpan.MaxValue;
        stats.Max = TimeSpan.MinValue;
        foreach (var time in _timingsQueue)
        {
            stats.Total += time;
            sumOfSquares += time.Pow2();
            if (time < stats.Min)
                stats.Min = time;
            if (time > stats.Max)
                stats.Max = time;
        }
        stats.Average = stats.Total / _timingsQueue.Count;
        stats.StdDev = TimeSpan.Zero.Max(sumOfSquares/_timingsQueue.Count - stats.Average.Pow2()).Sqrt();
        return stats;
    }
}

public static class TimeSpanExtensions
{
    public static TimeSpan Pow2(this TimeSpan span)
    {
        return new(span.Ticks * span.Ticks);
    }

    public static TimeSpan Max(this TimeSpan span, TimeSpan other)
    {
        return new(Math.Max(span.Ticks, other.Ticks));
    }

    public static TimeSpan Sqrt(this TimeSpan span)
    {
        return TimeSpan.FromMicroseconds(Math.Sqrt(span.TotalMicroseconds));
    }
}

public record struct TimelineStats
{
    public TimeSpan Total { get; set; }
    public TimeSpan Average { get; set; }
    public TimeSpan Min { get; set; }
    public TimeSpan Max { get; set; }
    public TimeSpan StdDev { get; set; }
}