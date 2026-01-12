using Avalonia.ReactiveUI;
using GameDotNet.Editor.ViewModels;
using GameDotNet.Graphics.Avalonia;
using Microsoft.Extensions.DependencyInjection;

namespace GameDotNet.Editor.Views;

public partial class EditorTabViewPortControl : ReactiveUserControl<EditorTabViewPortViewModel>
{
    public EditorTabViewPortControl()
    {
        InitializeComponent();
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        ViewportControl.Content = App.GetServiceProvider()?.GetRequiredService<SlangCompositionControl>();
    }
}