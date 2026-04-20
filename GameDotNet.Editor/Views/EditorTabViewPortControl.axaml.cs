using Avalonia.Platform;
using GameDotNet.Editor.ViewModels;
using GameDotNet.Graphics.Abstractions;
using GameDotNet.Graphics.Avalonia.Gpu.Interop;
using GameDotNet.Graphics.Vulkan;
using GameDotNet.Graphics.Vulkan.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ObjectPool;
using ReactiveUI.Avalonia;
using Shouldly;
using Size = System.Drawing.Size;

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

        //This only supports sharing the vk device and resources directly
        //I do not have a mac device but for macos we will most likely need to go the route of gpu/texture interop
        Content = sp.GetRequiredService<RenderThreadAnimationControl>();
    }
}
