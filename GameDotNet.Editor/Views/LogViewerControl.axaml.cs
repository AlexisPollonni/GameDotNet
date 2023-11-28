using Avalonia.ReactiveUI;
using GameDotNet.Editor.ViewModels;

namespace GameDotNet.Editor.Views;

public partial class LogViewerControl : ReactiveUserControl<LogViewerViewModel>
{
    public LogViewerControl()
    {
        InitializeComponent();
    }
}