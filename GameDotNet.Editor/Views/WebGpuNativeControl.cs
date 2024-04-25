using System;
using Avalonia.Controls;
using Avalonia.Platform;

namespace GameDotNet.Editor.Views;

public class WebGpuNativeControl : NativeControlHost
{
    public event Action<IPlatformHandle>? NativeControlCreated;
    public event Action<IPlatformHandle>? NativeControlDestroyed; 

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var ptr =  base.CreateNativeControlCore(parent);

        NativeControlCreated?.Invoke(ptr);
        
        return ptr;
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        NativeControlDestroyed?.Invoke(control);
        
        base.DestroyNativeControlCore(control);
    }
}