using GameDotNet.Core.Abstractions;
using GameDotNet.Graphics.Abstractions;
using GameDotNet.Graphics.Tooling;

namespace GameDotNet.Graphics.Models;

public readonly record struct RenderFrameRequest(IViewPort ViewPort, IDeviceTexture Texture);

public readonly record struct RenderFramePresentResponse(TimelineStats RenderStats);
