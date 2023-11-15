using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameDotNet.Editor.Tools;

[SuppressMessage("ReSharper", "TemplateIsNotCompileTimeConstantProblem")]
[SuppressMessage("Usage", "CA2254:Template should be a static expression")]
public class MicrosoftLogSink : ILogSink
{
    private readonly ConcurrentDictionary<Type, ILogger> _loggerCache;
    private readonly ILoggerFactory _factory;
    private readonly ILogger _defaultLogger;
    private readonly LogEventLevel _minimumLevel;
    private readonly IList<string>? _areas;

    public MicrosoftLogSink(ILoggerFactory factory, LogEventLevel minimumLevel, IList<string>? areas = null)
    {
        _factory = factory;
        _minimumLevel = minimumLevel;

        _loggerCache = new();
        _defaultLogger = factory.CreateLogger("Avalonia");
        _areas = areas?.Count > 0 ? areas : null;
    }

    public bool IsEnabled(LogEventLevel level, string area)
    {
        return level >= _minimumLevel && (_areas?.Contains(area) ?? true);
    }

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
    {
        if (IsEnabled(level, area))
        {
            // TODO: There might be a more efficient way to add the area string, to investigate
            GetLoggerOrCreateFromType(source?.GetType()).Log(LogEventLevelToMicrosoft(level), $"[{{Area}}] {messageTemplate}", area);
        }
    }

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
    {
        if (IsEnabled(level, area))
        {
            GetLoggerOrCreateFromType(source?.GetType()).Log(LogEventLevelToMicrosoft(level), $"[{{Area}}] {messageTemplate}", area, propertyValues);
        }
    }

    private ILogger GetLoggerOrCreateFromType(Type? type)
    {
        if (type is null) return _defaultLogger;
        if(_loggerCache.TryGetValue(type, out var logger))
            return logger;
        
        logger = _factory.CreateLogger(type);

        _loggerCache[type] = logger;
        return logger;
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