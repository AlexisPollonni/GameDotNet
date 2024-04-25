using Avalonia.Platform;
using Avalonia.ReactiveUI;
using GameDotNet.Editor.ViewModels;

namespace GameDotNet.Editor.Views;

public partial class WebGpuView : ReactiveUserControl<WebGpuViewModel>
{
    public WebGpuView()
    {
        InitializeComponent();
        
        NativeControl.NativeControlCreated += NativeControlOnNativeControlCreated;
        NativeControl.NativeControlDestroyed += NativeControlOnNativeControlDestroyed;
    }

    private void NativeControlOnNativeControlCreated(IPlatformHandle handle)
    {
        ViewModel!.SetMainView(new AvaloniaNativeView(NativeControl, handle));

        _ = ViewModel.Initialize();
    }

    private void NativeControlOnNativeControlDestroyed(IPlatformHandle obj)
    {
        //TODO: Destroy native resources when control destroyed (surface and swapchain)
    }
}