using Avalonia.ReactiveUI;
using GameDotNet.Editor.ViewModels;

namespace GameDotNet.Editor.Views;

internal sealed partial class EntityInspectorControl : ReactiveUserControl<EntityInspectorViewModel>
{
    public EntityInspectorControl()
    {
        InitializeComponent();
    }
}