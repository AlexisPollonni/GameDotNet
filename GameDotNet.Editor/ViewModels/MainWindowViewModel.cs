namespace GameDotNet.Editor.ViewModels;

internal class MainWindowViewModel(EditorTabViewPortViewModel editorViewPort, EntityTreeViewModel treeViewModel, LogViewerViewModel logViewModel, EntityInspectorViewModel inspectorViewModel) : ViewModelBase
{
    public EditorTabViewPortViewModel EditorViewPort { get; } = editorViewPort;
    public EntityTreeViewModel TreeViewModel { get; } = treeViewModel;
    public EntityInspectorViewModel InspectorViewModel { get; } = inspectorViewModel;
    public LogViewerViewModel LogViewModel { get; } = logViewModel;
}