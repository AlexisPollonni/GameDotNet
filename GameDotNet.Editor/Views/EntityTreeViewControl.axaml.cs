using GameDotNet.Editor.ViewModels;
using ReactiveUI.Avalonia;

namespace GameDotNet.Editor.Views;

public partial class EntityTreeViewControl : ReactiveUserControl<EntityTreeViewModel>
{
    public EntityTreeViewControl()
    {
        InitializeComponent();
    }
}
