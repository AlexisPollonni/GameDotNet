using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.ReactiveUI;
using DynamicData;
using Microsoft.Extensions.Logging;

namespace GameDotNet.Editor.ViewModels;

[RegisterSingleton(Registration = RegistrationStrategy.Self)]
public sealed class LogViewerViewModel : ViewModelBase
{
    public ReadOnlyObservableCollection<LogEntryViewModel>? LogEntries { get; set; }
    
    private readonly SourceList<LogEntryViewModel> _logEventCache = new();

    public override void OnActivated(CompositeDisposable disposable)
    {
        base.OnActivated(disposable);
        
        _logEventCache.Connect()
            .ObserveOn(AvaloniaScheduler.Instance)
            .Bind(out var collection)
            .Subscribe()
            .DisposeWith(disposable);

        LogEntries = collection;
    }

    public void EmitStandard(LogLevel level, string message)
    {
        _logEventCache.Add(new(DateTimeOffset.Now, level, message));
    }

    protected override void Dispose(bool disposing)
    {
        _logEventCache.Dispose();
        
        base.Dispose(disposing);
    }
}

public sealed record LogEntryViewModel(DateTimeOffset TimeStamp, LogLevel Level, string Message)
{
    public DateTimeOffset TimeStamp { get; } = TimeStamp;
    public LogLevel Level { get; } = Level;
    public string Message { get; } = Message;
}