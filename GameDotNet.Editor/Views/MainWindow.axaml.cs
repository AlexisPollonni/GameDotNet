using Avalonia;
using Avalonia.Controls;
using Avalonia.Rendering;

namespace GameDotNet.Editor.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.AttachDevTools();
            Renderer.Diagnostics.DebugOverlays = RendererDebugOverlays.Fps;
        }
    }
}