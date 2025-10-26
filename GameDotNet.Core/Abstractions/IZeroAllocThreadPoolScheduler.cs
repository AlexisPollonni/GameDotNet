using System.Runtime.CompilerServices;

namespace GameDotNet.Core.Abstractions;

public interface IZeroAllocThreadPoolScheduler<TUserState> where TUserState : struct
{
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    public ValueTask EnqueueWork(Func<TUserState, CancellationToken, ValueTask> work,
                                 TUserState userState,
                                 CancellationToken token = default);
}