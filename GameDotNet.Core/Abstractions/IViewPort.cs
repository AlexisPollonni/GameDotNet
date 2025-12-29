using System.Drawing;

namespace GameDotNet.Core.Abstractions;

public interface IViewPort
{
    IAsyncEnumerable<Size> Resized { get; }
    IAsyncEnumerable<bool> FocusAcquired { get; }

    Size Size { get; }
    bool IsActive { get; }
    
    IInputContext Input { get; }
}