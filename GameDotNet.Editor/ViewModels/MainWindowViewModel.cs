namespace GameDotNet.Editor.ViewModels;

internal class MainWindowViewModel(EditorTabViewPortViewModel webGpuViewModel, EntityTreeViewModel treeViewModel, LogViewerViewModel logViewModel, EntityInspectorViewModel inspectorViewModel) : ViewModelBase
{
    public EditorTabViewPortViewModel WebGpuViewModel { get; } = webGpuViewModel;
    public EntityTreeViewModel TreeViewModel { get; } = treeViewModel;
    public EntityInspectorViewModel InspectorViewModel { get; } = inspectorViewModel;
    public LogViewerViewModel LogViewModel { get; } = logViewModel;
}