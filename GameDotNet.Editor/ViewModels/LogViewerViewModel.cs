using System;
using System.Collections.ObjectModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Avalonia.ReactiveUI;
using DynamicData;
using DynamicData.Alias;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace GameDotNet.Editor.ViewModels;

public sealed class LogViewerViewModel : ViewModelBase, ILogEventSink, IDisposable
{
    public ReadOnlyObservableCollection<LogEntryViewModel> LogEntries { get; set; }
    
    
    private readonly SourceList<LogEvent> _logEventCache;
    private readonly IDisposable _disposable;


    public LogViewerViewModel()
    {
        _logEventCache = new();

        _disposable = _logEventCache.Connect()
                      .ObserveOn(Scheduler.Default)
                      .Select(EventLogToViewModel)
                      .ObserveOn(AvaloniaScheduler.Instance)
                      .Bind(out var collection)
                      .Subscribe();

        LogEntries = collection;
    }

    public void Emit(LogEvent logEvent)
    {
        _logEventCache.Add(logEvent);
    }

    public void Dispose()
    {
        _disposable.Dispose();
        _logEventCache.Dispose();
    }
    
    private static LogEntryViewModel EventLogToViewModel(LogEvent e)
    {
        return new(e.Timestamp, e.Level, e.RenderMessage());
    }
}

public sealed record LogEntryViewModel(DateTimeOffset TimeStamp, LogEventLevel Level, string Message)
{
    public DateTimeOffset TimeStamp { get; } = TimeStamp;
    public LogEventLevel Level { get; } = Level;
    public string Message { get; } = Message;
}

public static class SinkExtensions
{
    public static LoggerConfiguration LogViewSink(this LoggerSinkConfiguration configuration, LogViewerViewModel vm)
    {
        return configuration.Sink(vm);
    }
}