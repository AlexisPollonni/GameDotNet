using System;
using System.Collections.Concurrent;
using GameDotNet.Editor.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameDotNet.Editor.Tools;

public sealed class ViewerLogProvider(LogViewerViewModel viewer) : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, ViewerLogger> _loggers = new();

    public ILogger CreateLogger(string categoryName)
    {
        var l = new ViewerLogger(categoryName, viewer);
        if (!_loggers.TryAdd(categoryName, l))
        {
            throw new NotSupportedException($"2 loggers cannot have the same category {categoryName}");
        }

        return l;
    }

    public void Dispose()
    {
        _loggers.Clear();
    }
}


public class ViewerLogger(string category, LogViewerViewModel viewer) : ILogger
{
    private readonly string _category = category;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return default;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        viewer.EmitStandard(logLevel, formatter(state, exception));
    }
}

public static class LoggerServiceExtensions
{
    public static IServiceCollection AddViewerLogging(this IServiceCollection services)
    {
        services.AddSingleton<ILoggerProvider, ViewerLogProvider>();

        return services;
    }
}