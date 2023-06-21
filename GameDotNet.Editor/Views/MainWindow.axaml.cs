using Avalonia.Controls;

namespace GameDotNet.Editor.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Renderer.DrawFps = true;
        }
    }
}