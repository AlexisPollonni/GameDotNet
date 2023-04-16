using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Reactive;
using Avalonia.Rendering.Composition;

namespace Avalonia.Rendering;

/// <summary>
/// A helper class for composition-backed swapchains, should not be a public API yet
/// </summary>
internal abstract class SwapchainBase<TImage> : IAsyncDisposable where TImage : class, ISwapchainImage
{
    protected ICompositionGpuInterop Interop { get; }
    protected CompositionDrawingSurface Target { get; }
    private List<TImage> _pendingImages = new();

    public SwapchainBase(ICompositionGpuInterop interop, CompositionDrawingSurface target)
    {
        Interop = interop;
        Target = target;
    }

    private static bool IsBroken(TImage image) => image.LastPresent?.IsFaulted == true;
    private static bool IsReady(TImage image) => image.LastPresent == null || image.LastPresent.Status == TaskStatus.RanToCompletion;

    private TImage? CleanupAndFindNextImage(PixelSize size)
    {
        TImage? firstFound = null;
        var foundMultiple = false;
        
        for (var c = _pendingImages.Count - 1; c > -1; c--)
        {
            var image = _pendingImages[c];
            var ready = IsReady(image);
            var matches = image.Size == size;
            if (IsBroken(image) || (!matches && ready))
            {
                image.DisposeAsync();
                _pendingImages.RemoveAt(c);
            }

            if (matches && ready)
            {
                if (firstFound == null)
                    firstFound = image;
                else
                    foundMultiple = true;
            }

        }

        // We are making sure that there was at least one image of the same size in flight
        // Otherwise we might encounter UI thread lockups
        return foundMultiple ? firstFound : null;
    }

    protected abstract TImage CreateImage(PixelSize size);

    protected IDisposable BeginDrawCore(PixelSize size, out TImage image)
    {
        var img = CleanupAndFindNextImage(size) ?? CreateImage(size);
        
        img.BeginDraw();
        _pendingImages.Remove(img);
        image = img;
        
        return new AnonymousDisposable(() =>
        {
            img.Present();
            _pendingImages.Add(img);
        });
    }
    
    public async ValueTask DisposeAsync()
    {
        foreach (var img in _pendingImages)
            await img.DisposeAsync();
    }
}

internal interface ISwapchainImage : IAsyncDisposable
{
    PixelSize Size { get; }
    Task? LastPresent { get; }
    void BeginDraw();
    void Present();
}


internal sealed class AnonymousDisposable : IDisposable
{
    private volatile Action? _dispose;
    public AnonymousDisposable(Action dispose)
    {
        _dispose = dispose;
    }
    public bool IsDisposed => _dispose == null;
    public void Dispose()
    {
        Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}

