using System.Runtime.CompilerServices;
using GameDotNet.Core.Abstractions;
using GameDotNet.Core.Tooling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Shouldly;

namespace GameDotNet.Core.Services;

internal sealed class ResettableWorkItem<TUserState>(ILogger<ResettableWorkItem<TUserState>> logger) : IThreadPoolWorkItem, IResettable 
{
    // ReSharper disable once MemberCanBePrivate.Global
    // Incorrect Rider suggestion.
    // Cannot make private because of property WorkValue. Causes CS0053
    internal readonly record struct JobItem(
        Func<TUserState, CancellationToken, ValueTask> Work,
        TUserState UserState,
        PooledValueTaskSource Source,
        CancellationToken Token);

    internal JobItem? WorkValue { get; set; }

    public void Execute()
    {
        WorkValue.ShouldNotBeNull("WorkValue was not set before execution.");
        
        var item = WorkValue.Value;
        
        try
        {
            if (item.Token.IsCancellationRequested)
            {
                item.Source.SetException(new OperationCanceledException(item.Token));
                return;
            }

#pragma warning disable CA2252
#pragma warning disable SYSLIB5007
            AsyncHelpers.Await(item.Work(item.UserState, item.Token));
#pragma warning restore SYSLIB5007
#pragma warning restore CA2252

            item.Source.SetResult();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception occurred while executing thread pool work item");
            item.Source.SetException(ex);
        }
    }

    public bool TryReset()
    {
        WorkValue = null;
        return true;
    }
}

[RegisterSingleton]
//TODO: Right now uses 2 object pools for work item and vts, but could be combined into one with a unique work item type.
internal sealed class PooledThreadPoolScheduler<TUserState>(
    ObjectPool<PooledValueTaskSource> valueTaskSourcePool,
    ObjectPool<ResettableWorkItem<TUserState>> workItemPool) : IZeroAllocThreadPoolScheduler<TUserState>
{
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    public async ValueTask EnqueueWork(Func<TUserState, CancellationToken, ValueTask> work,
                                       TUserState userState,
                                       CancellationToken token = default)
    {
        work.ShouldNotBeNull();

        var valueTaskSource = valueTaskSourcePool.Get();
        var workItem = workItemPool.Get();
        
        workItem.WorkValue = new(work, userState, valueTaskSource, token);
        
        ThreadPool.UnsafeQueueUserWorkItem(workItem, preferLocal: false);
        
        try
        {
            await valueTaskSource.AsValueTask().ConfigureAwait(false);
        }
        finally
        {
            workItemPool.Return(workItem);
            valueTaskSourcePool.Return(valueTaskSource);
        }
    }
}

