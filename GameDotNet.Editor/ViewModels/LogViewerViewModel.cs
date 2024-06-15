using System;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Avalonia.ReactiveUI;
using DynamicData;
using Microsoft.Extensions.Logging;

namespace GameDotNet.Editor.ViewModels;

public sealed class LogViewerViewModel : ViewModelBase, IDisposable
{
    public ReadOnlyObservableCollection<LogEntryViewModel> LogEntries { get; set; }
    
    
    private readonly SourceList<LogEntryViewModel> _logEventCache;
    private readonly IDisposable _disposable;


    public LogViewerViewModel()
    {
        _logEventCache = new();

        _disposable = _logEventCache.Connect()
                      .ObserveOn(AvaloniaScheduler.Instance)
                      .Bind(out var collection)
                      .Subscribe();

        LogEntries = collection;
    }

    public void EmitStandard(LogLevel level, string message)
    {
        _logEventCache.Add(new(DateTimeOffset.Now, level, message));
    }

    public void Dispose()
    {
        _disposable.Dispose();
        _logEventCache.Dispose();
    }
}

public sealed record LogEntryViewModel(DateTimeOffset TimeStamp, LogLevel Level, string Message)
{
    public DateTimeOffset TimeStamp { get; } = TimeStamp;
    public LogLevel Level { get; } = Level;
    public string Message { get; } = Message;
}