using Arch.Core;
using GameDotNet.Graphics.Tooling;

namespace GameDotNet.Graphics.Abstractions;

public interface IEntityRenderer
{
    ValueTask<TimelineStats> Render(IDeviceTexture renderTarget);
}
