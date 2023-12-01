using System.Drawing;
using MessagePipe;
using Silk.NET.Core.Contexts;
using IInputContext = GameDotNet.Input.Abstract.IInputContext;

namespace GameDotNet.Graphics.Abstractions;

public interface INativeView
{
    public ISubscriber<Size> Resized { get; }
    public ISubscriber<bool> FocusChanged { get; }

    public Size Size { get; }
    
    public IInputContext Input { get; }
    
    public bool IsClosing { get; }
    
    public INativeWindow? Native { get; }
}