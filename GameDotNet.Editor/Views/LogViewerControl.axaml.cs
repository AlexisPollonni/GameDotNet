using GameDotNet.Editor.ViewModels;
using ReactiveUI.Avalonia;

namespace GameDotNet.Editor.Views;

public partial class LogViewerControl : ReactiveUserControl<LogViewerViewModel>
{
    public LogViewerControl()
    {
        InitializeComponent();
    }
}
