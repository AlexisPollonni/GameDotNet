using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using Arch.Core;
using GameDotNet.Core.Abstractions;
using GameDotNet.Core.Tooling;
using GameDotNet.Core.Tooling.Extensions;
using GameDotNet.Graphics.Models;
using MessagePipe;
using Microsoft.Extensions.ObjectPool;
using ShaderSlang.Net.Bindings.Generated;
using IFence = ShaderSlang.Net.ComWrappers.Gfx.Interfaces.IFence;
using IResourceView = ShaderSlang.Net.ComWrappers.Gfx.Interfaces.IResourceView;

namespace GameDotNet.Graphics.Services;

[RegisterSingleton]
public class SlangRenderer(SlangContext context, ObjectPool<PooledValueTaskSource> valueTaskSourcePool)
    : IQueryUpdateJob, IAsyncRequestHandler<RenderFrameRequest, RenderFramePresentResponse>
{
    public ValueTask OnUpdate(TimeSpan deltaTime, CancellationToken cancellationToken = default)
    {
        lock (_fenceLock)
        {
            foreach (var tuple in _inflightFences)
            {
                var (fence, taskSource, signalValue) = tuple;
                if (fence.GetCurrentValueOrThrow() == signalValue)
                {
                    taskSource.SetResult();
                }
            }
        }

        return ValueTask.CompletedTask;
    }

    public bool IsStarted { get; set; }
    public JobConfiguration Options { get; } = new();
    public QueryDescription Query { get; } = new(); //TODO: Define what this system queries for


    private readonly List<(IFence, PooledValueTaskSource, ulong)> _inflightFences = [];
    private readonly Lock _fenceLock = new();


    public async ValueTask<RenderFramePresentResponse> InvokeAsync(RenderFrameRequest request,
        CancellationToken cancellationToken = default)
    {
        var device = context.Device;
        var texture = request.Image;

        var textureView = device.CreateTextureViewOrThrow(texture, new());

        var color = Color.FromArgb((int)DateTimeOffset.UtcNow.Ticks);

        await ClearTextureAsync(textureView, color, cancellationToken);

        return new();

        //TODO: Implement actual rendering here
        // var frameBufferLayout =
        //     device.CreateFramebufferLayoutOrThrow(new([new() { format = texture.GetDesc().Format, sampleCount = 0 }],
        //         null));
        //
        // IReadOnlyList<IRenderPassLayout.TargetAccessDesc> renderTargetAccesses =
        //     [new() { initialState = ResourceState.Undefined, finalState = ResourceState.RenderTarget }];
        // var renderPassLayout = device.CreateRenderPassLayoutOrThrow(new(frameBufferLayout, renderTargetAccesses, null));
        //
        //
        // var framebuffer = device.CreateFramebufferOrThrow(new([textureView], null, frameBufferLayout))
        //
        // cmd.EncodeRenderCommands(renderPassLayout, framebuffer, out var renderEncoder);
        //
        // renderEncoder.renderEncoder.EndEncoding();

        //cmd.Close();
    }

    public async ValueTask WaitForFenceAsync(IFence fence, ulong signalValue = 1,
        CancellationToken cancellationToken = default)
    {
        var tcs = valueTaskSourcePool.Get();
        var tuple = (fence, tcs, signalValue);

        cancellationToken.Register((o, innerToken) =>
        {
            var innerTcs = (PooledValueTaskSource)o!;
            var e = new OperationCanceledException(innerToken);

            innerTcs.SetException(e);
        }, tcs);
        
        try
        {
            lock (_fenceLock)
            {
                _inflightFences.Add(tuple);
            }

            await tcs.AsValueTask().ConfigureAwait(false);
        }
        finally
        {
            valueTaskSourcePool.Return(tcs);

            lock (_fenceLock)
            {
                _inflightFences.Remove(tuple);
            }
        }
    }

    private async ValueTask ClearTextureAsync(IResourceView texture, Color clearColor, CancellationToken token = default)
    {
        var cmd = context.TransientHeap.CreateCommandBufferOrThrow();

        cmd.EncodeResourceCommands(out var encoder);

        var clearValue = clearColor.ToColorClearValue();

        encoder.ClearResourceView(texture, new() { color = clearValue }, ClearResourceViewFlags.Enum.ClearStencil);

        cmd.Close();
        
        var fence = context.Device.CreateFenceOrThrow(new());
        context.GraphicsQueue.ExecuteCommandBuffers([cmd], fence, 1);

        await WaitForFenceAsync(fence, 1, token);
    }
}

public static class ColorExtensions
{
    public static ColorClearValue ToColorClearValue(this Color color)
    {
        var vec = color.ToVector4();
        ref var values = ref Unsafe.As<Vector4, ColorClearValue._floatValues_e__FixedBuffer>(ref vec);

        return new()
        {
            floatValues = values
        };
    }
}