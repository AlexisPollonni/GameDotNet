using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform;
using GameDotNet.Editor.Tools;
using GameDotNet.Graphics;
using MessagePipe;
using Microsoft.Extensions.DependencyInjection;

namespace GameDotNet.Editor.Views;

public class WebGpuNativeControl : NativeControlHost
{

    private readonly TaskCompletionSource<IPlatformHandle> _createCts = new();

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var ptr =  base.CreateNativeControlCore(parent);

        _createCts.SetResult(ptr);
        
        return ptr;
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        _createCts.TrySetCanceled();
        //TODO: Destroy native resources when control destroyed (surface and swapchain)
        
        base.DestroyNativeControlCore(control);
    }

    public async Task Initialize()
    {
        var provider = App.GetServiceProvider();
        var factory =  provider?.GetRequiredService<EventFactory>();
        var viewManager = provider?.GetRequiredService<NativeViewManager>();
        
        var handle = await _createCts.Task;
        
        var nativeView = new AvaloniaNativeView(this, handle, factory!);
        
        viewManager!.MainView = nativeView;
    }
}