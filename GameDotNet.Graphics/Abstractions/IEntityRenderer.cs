using Arch.Core;
using GameDotNet.Graphics.Tooling;

namespace GameDotNet.Graphics.Abstractions;

public interface IEntityRenderer
{
    TimelineStats Render(IDeviceTexture renderTarget, params ReadOnlySpan<Entity> entities);
}
