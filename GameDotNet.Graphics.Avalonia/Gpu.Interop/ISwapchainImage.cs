using Avalonia;

namespace GameDotNet.Graphics.Avalonia.Gpu.Interop;

public interface ISwapchainImage : IAsyncDisposable
{
    PixelSize Size { get; }
    Task? LastPresent { get; }
    void BeginDraw();
    void Present();
}