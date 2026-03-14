using GameDotNet.Editor.ViewModels;
using GameDotNet.Graphics.Avalonia;
using GameDotNet.Graphics.Vulkan;
using GameDotNet.Graphics.Vulkan.Wrappers;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Avalonia;
using Shouldly;

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

        var sp = App.GetServiceProvider().ShouldNotBeNull();

        var compositionControl = sp.GetRequiredService<RendererCompositionControl>();

        compositionControl.SwapchainFactory = (interop, target) =>
            new VulkanAvaloniaSwapchain(interop, target, sp.GetRequiredService<IVulkanContext>());

        Content = compositionControl;
    }
}
