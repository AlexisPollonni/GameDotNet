using System.Collections.Concurrent;
using Arch.Core;
using GameDotNet.Core.Abstractions;
using GameDotNet.Graphics.Abstractions;
using GameDotNet.Graphics.Models;
using GameDotNet.Graphics.Tooling;
using MessagePipe;

namespace GameDotNet.Graphics.Services;

[RegisterSingleton]
public class SlangRenderer(IEntityRenderer renderer)
    : IQueryUpdateJob,
        IAsyncRequestHandler<RenderFrameRequest, RenderFramePresentResponse>
{
    public ValueTask OnUpdate(TimeSpan deltaTime, CancellationToken cancellationToken = default)
    {
        // lock (_fenceLock)
        // {
        //     foreach (var tuple in _inflightFences)
        //     {
        //         var (fence, taskSource, signalValue) = tuple;
        //         if (fence.GetCurrentValueOrThrow() == signalValue)
        //         {
        //             taskSource.SetResult();
        //         }
        //     }
        // }

        return ValueTask.CompletedTask;
    }

    public bool IsStarted { get; set; }
    public JobConfiguration Options { get; } = new();
    public QueryDescription Query { get; } = new(); //TODO: Define what this system queries for

    private readonly ConcurrentDictionary<IViewPort, TimingsRingBuffer> _viewportTimings = new();
    private readonly Lock _fenceLock = new();

    public async ValueTask<RenderFramePresentResponse> InvokeAsync(
        RenderFrameRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var stats = renderer.Render(request.Texture);

        return new(stats);
    }
}
