using System.Drawing;
using GameDotNet.Core.Abstractions;
using GameDotNet.Graphics.Tooling;

namespace GameDotNet.Graphics.Models;

public readonly record struct RenderFrameRequest(IViewPort ViewPort, ITextureResource Image, Size Size);
public readonly record struct RenderFramePresentResponse(TimelineStats RenderStats);