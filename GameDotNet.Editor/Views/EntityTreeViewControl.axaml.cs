using Avalonia.ReactiveUI;
using GameDotNet.Editor.ViewModels;

namespace GameDotNet.Editor.Views;

public partial class EntityTreeViewControl : ReactiveUserControl<EntityTreeViewModel>
{
    public EntityTreeViewControl()
    {
        InitializeComponent();
    }
}