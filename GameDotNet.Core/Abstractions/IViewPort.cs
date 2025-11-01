using System.Drawing;
using MessagePipe;

namespace GameDotNet.Core.Abstractions;

public interface IViewPort
{
    ISubscriber<Size> Resized { get; }
    ISubscriber<bool> FocusAcquired { get; }

    Size Size { get; }
    bool IsActive { get; }
    
    IInputContext Input { get; }
}