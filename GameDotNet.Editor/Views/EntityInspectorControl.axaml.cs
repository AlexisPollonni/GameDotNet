using GameDotNet.Editor.ViewModels;
using ReactiveUI.Avalonia;

namespace GameDotNet.Editor.Views;

internal sealed partial class EntityInspectorControl : ReactiveUserControl<EntityInspectorViewModel>
{
    public EntityInspectorControl()
    {
        InitializeComponent();
    }
}
