using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace GameDotNet.Core.Tools.Extensions;

public static class ReactiveExtensions
{
    public static async Task StartAsync(this IScheduler scheduler, Action action, CancellationToken token = default)
    {
        await Observable.Start(action, scheduler).RunAsync(token);
    }

    public static async Task StartAsync(this IScheduler scheduler, Func<CancellationToken, Task> func, CancellationToken token = default)
    {
        await Observable.FromAsync(func, scheduler).SubscribeOn(scheduler).RunAsync(token);
    }
}