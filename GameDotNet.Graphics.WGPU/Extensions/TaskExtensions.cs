using GameDotNet.Graphics.WGPU.Wrappers;

namespace GameDotNet.Graphics.WGPU.Extensions;

public static class TaskExtensions
{
    public static async Task<T> WaitWhilePollingAsync<T>(this Task<T> task, Instance instance, CancellationToken token = default)
    {
        var pollTask = Task.Run(() =>
        {
            while (!task.IsCompleted)
            {
                Task.Delay(20, token).Wait(token);
                instance.ProcessEvents();
            }
        }, token);

        var res = await task;
        await pollTask;

        return res;
    }    
    
    public static async ValueTask<T> WaitWhilePollingAsync<T>(this ValueTask<T> task, Instance instance, CancellationToken token = default)
    {
        var pollTask = Task.Run(() =>
        {
            while (!task.IsCompleted)
            {
                Task.Delay(20, token).Wait(token);
                instance.ProcessEvents();
            }
        }, token);

        var res = await task;
        await pollTask;

        return res;
    }    
    
    public static ValueTask<T> WaitWhilePollingAsync<T>(this ValueTask<T> task, WebGpuContext ctx, CancellationToken token = default)
    {
        return task.WaitWhilePollingAsync(ctx.Instance, token);
    }
}