using System.Diagnostics.CodeAnalysis;
using Avalonia.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameDotNet.Editor.Tools;

[SuppressMessage("ReSharper", "TemplateIsNotCompileTimeConstantProblem")]
[SuppressMessage("Usage", "CA2254:Template should be a static expression")]
public class MicrosoftLogSink(ILoggerFactory factory, LogEventLevel minimumLevel, IList<string>? areas = null)
    : ILogSink
{
    private readonly ILogger _logger = factory.CreateLogger("Avalonia");
    private readonly string[]? _areas = areas?.Count > 0 ? areas.ToArray() : null;
    private readonly Lock _lock = new();

    public bool IsEnabled(LogEventLevel level, string area)
    {
        return level >= minimumLevel && (_areas?.Contains(area) ?? true);
    }

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
    {
        if (!IsEnabled(level, area)) return;
        
        var l = LogEventLevelToMicrosoft(level);
        
        using var a = _logger.BeginScope(area);
        if (source is null)
        {
            _logger.Log(l, messageTemplate);
            return;
        }
            
        using var s = _logger.BeginScope(source.GetType());
            
        _logger.Log(l, messageTemplate);
    }
    

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
    {
        if (!IsEnabled(level, area)) return;
        var l = LogEventLevelToMicrosoft(level);
        
        using var a = _logger.BeginScope(area);
        
        if (source is null)
        {
            _logger.Log(l, messageTemplate, propertyValues);
            return;
        }
            
        using var s = _logger.BeginScope(source.GetType());
            
        _logger.Log(l, messageTemplate, propertyValues);
    }

    private static LogLevel LogEventLevelToMicrosoft(LogEventLevel lvl) =>
        lvl switch
        {
            LogEventLevel.Verbose => LogLevel.Trace,
            LogEventLevel.Debug => LogLevel.Debug,
            LogEventLevel.Information => LogLevel.Information,
            LogEventLevel.Warning => LogLevel.Warning,
            LogEventLevel.Error => LogLevel.Error,
            LogEventLevel.Fatal => LogLevel.Critical,
            _ => throw new ArgumentOutOfRangeException(nameof(lvl), lvl, null)
        };
}

public static class LoggingExtensions
{
    public static IServiceCollection AddAvaloniaLogger(this IServiceCollection col, LogEventLevel minimumLevel, params string[] areas)
    {
        col.AddTransient<ILogSink, MicrosoftLogSink>(p => new(p.GetRequiredService<ILoggerFactory>(), minimumLevel,
                                                            areas));
        return col;
    }
}