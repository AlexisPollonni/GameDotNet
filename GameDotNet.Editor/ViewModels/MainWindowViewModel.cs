namespace GameDotNet.Editor.ViewModels
{
    public class MainWindowViewModel(WebGpuViewModel webGpuViewModel) : ViewModelBase
    {
        public WebGpuViewModel WebGpuViewModel { get; } = webGpuViewModel;
    }
}