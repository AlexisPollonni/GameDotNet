namespace GameDotNet.Editor.ViewModels
{
    public class MainWindowViewModel(WebGpuViewModel webGpuViewModel, EntityTreeViewModel treeViewModel, LogViewerViewModel LogViewModel, EntityInspectorViewModel InspectorViewModel) : ViewModelBase
    {
        public WebGpuViewModel WebGpuViewModel { get; } = webGpuViewModel;
        public EntityTreeViewModel TreeViewModel { get; } = treeViewModel;
        public EntityInspectorViewModel InspectorViewModel { get; } = InspectorViewModel;
        public LogViewerViewModel LogViewModel { get; } = LogViewModel;
    }
}