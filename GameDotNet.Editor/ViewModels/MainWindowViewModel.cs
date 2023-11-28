namespace GameDotNet.Editor.ViewModels
{
    public class MainWindowViewModel(WebGpuViewModel webGpuViewModel, EntityTreeViewModel treeViewModel, LogViewerViewModel LogViewModel) : ViewModelBase
    {
        public WebGpuViewModel WebGpuViewModel { get; } = webGpuViewModel;
        public EntityTreeViewModel TreeViewModel { get; } = treeViewModel;
        public LogViewerViewModel LogViewModel { get; } = LogViewModel;
    }
}