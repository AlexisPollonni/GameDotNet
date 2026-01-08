using Avalonia.ReactiveUI;
using Avalonia.Rendering;
using GameDotNet.Editor.ViewModels;

namespace GameDotNet.Editor.Views
{
    internal partial class MainWindow : ReactiveWindow<MainWindowViewModel>
    {
        public MainWindow()
        {
            InitializeComponent();
            RendererDiagnostics.DebugOverlays = RendererDebugOverlays.Fps;
        }
    }
}