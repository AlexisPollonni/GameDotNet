using Avalonia;
using Avalonia.Rendering.Composition;
using GameDotNet.Core.Tooling;
using Nito.Disposables;

namespace GameDotNet.Graphics.Avalonia.Gpu.Interop;

/// <summary>
/// A helper class for composition-backed swapchains, should not be a public API yet
/// </summary>
public abstract class SwapchainBase(
    ICompositionGpuInterop interop,
    CompositionDrawingSurface target
) : SingleNonblockingAsyncDisposable<EmptyStruct>(default)
{
    protected ICompositionGpuInterop Interop { get; } = interop;
    protected CompositionDrawingSurface Target { get; } = target;
    private readonly List<ISwapchainImage> _pendingImages = [];

    private static bool IsBroken(ISwapchainImage image) => image.LastPresent?.IsFaulted == true;

    private static bool IsReady(ISwapchainImage image) =>
        image.LastPresent == null || image.LastPresent.Status == TaskStatus.RanToCompletion;

    private ISwapchainImage? CleanupAndFindNextImage(PixelSize size)
    {
        ISwapchainImage? firstFound = null;
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

    protected abstract ISwapchainImage CreateImage(PixelSize size);

    public abstract IDisposable BeginDraw(PixelSize size, out ISwapchainImage image);

    protected IDisposable BeginDrawCore(PixelSize size, out ISwapchainImage image)
    {
        var img = CleanupAndFindNextImage(size) ?? CreateImage(size);

        img.BeginDraw();
        _pendingImages.Remove(img);
        image = img;
        return Disposable.Create(() =>
        {
            img.Present();
            _pendingImages.Add(img);
        });
    }

    protected override async ValueTask DisposeAsync(EmptyStruct context)
    {
        foreach (var img in _pendingImages)
            await img.DisposeAsync();
    }
}
