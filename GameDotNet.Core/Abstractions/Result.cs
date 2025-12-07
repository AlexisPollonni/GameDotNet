using dotVariant;

namespace GameDotNet.Core.Abstractions;

public readonly record struct Error(string Message, Exception? Exception = null);

[Variant]
public readonly partial struct Result<T>
{
    static partial void VariantOf(T success, Error failure);
}

public static class Result
{
    // Synchronous factory methods
    public static Result<T> Success<T>(T value) => new(value);

    public static Result<T> Failure<T>(Error error) => new(error);

    public static Result<T> Failure<T>(string message, Exception? exception = null) =>
        Failure<T>(new Error(message, exception));

    public static Result<T> Failure<T>(Exception exception) =>
        Failure<T>(new Error("Exception was thrown", exception));

    internal static Result<T> FromUninitialized<T>() =>
        Failure<T>("An uninitialized Result was accessed.");

    // Asynchronous factory methods

    public static ValueTask<Result<T>> SuccessAsync<T>(T value) => new(new Result<T>(value));

    public static ValueTask<Result<T>> FailureAsync<T>(Error error) => new(new Result<T>(error));

    public static ValueTask<Result<T>> FromAsync<T>(Result<T> result) => new(result);

    public static async ValueTask<Result<T>> FromAsync<T>(ValueTask<T> task)
    {
        try
        {
            var res = await task;
            return Success(res);
        }
        catch (Exception e)
        {
            return Failure<T>("Exception thrown during ValueTask execution", e);
        }
    }

    public static async ValueTask<Result<T>> FromAsync<T>(Task<T> task)
    {
        try
        {
            var res = await task;
            return Success(res);
        }
        catch (Exception e)
        {
            return Failure<T>("Exception thrown during Task execution", e);
        }
    }
}

public static class ResultExtensions
{
    extension<T>(Result<T> result)
    {
        public Result<TResult> Map<TResult>(Func<T, TResult> mapper)
        {
            if (result.TryMatch(out T? success) && success is not null)
            {
                return Result.Success(mapper(success));
            }

            if (result.TryMatch(out Error failure))
            {
                return Result.Failure<TResult>(failure);
            }

            return Result.FromUninitialized<TResult>();
        }

        public Result<TResult> Bind<TResult>(Func<T, Result<TResult>> binder)
        {
            if (result.TryMatch(out T? success) && success is not null)
            {
                return binder(success);
            }

            if (result.TryMatch(out Error failure))
            {
                return Result.Failure<TResult>(failure);
            }

            return Result.FromUninitialized<TResult>();
        }

        public Result<T> OnSuccess(Action<T> action)
        {
            if (result.TryMatch(out T? success) && success is not null)
            {
                action(success);
            }
            return result;
        }

        public Result<T> OnFailure(Action<Error> action)
        {
            if (result.TryMatch(out Error failure))
            {
                action(failure);
            }
            return result;
        }

        public T Unwrap()
        {
            if (result.TryMatch(out T? success) && success is not null)
            {
                return success;
            }

            if (result.TryMatch(out Error failure))
            {
                throw new InvalidOperationException($"Result was a failure: {failure.Message}", failure.Exception);
            }

            throw new InvalidOperationException("Result was uninitialized.");
        }
    }
    
    
    extension<T>(ValueTask<Result<T>> asyncResult)
    {
        /// <summary>
        /// Asynchronously chains two operations.
        /// If the initial result is a success, the binder function is called with the success value.
        /// If the initial result is a failure, the failure is propagated.
        /// </summary>
        public async ValueTask<Result<TResult>> BindAsync<TResult>(Func<T, ValueTask<Result<TResult>>> binder)
        {
            var initialResult = await asyncResult.ConfigureAwait(false);

            if (initialResult.TryMatch(out T? success) && success is not null)
            {
                return await binder(success).ConfigureAwait(false);
            }

            if (initialResult.TryMatch(out Error failure))
            {
                return Result.Failure<TResult>(failure);
            }

            return Result.FromUninitialized<TResult>();
        }

        public async ValueTask<Result<TResult>> BindAsync<TResult>(Func<T, CancellationToken, ValueTask<Result<TResult>>> binder, CancellationToken cancellationToken)
        {
            var initialResult = await asyncResult.ConfigureAwait(false);

            if (initialResult.TryMatch(out T? success) && success is not null)
            {
                return await binder(success, cancellationToken).ConfigureAwait(false);
            }

            if (initialResult.TryMatch(out Error failure))
            {
                return Result.Failure<TResult>(failure);
            }

            return Result.FromUninitialized<TResult>();
        }

        public async ValueTask<Result<TResult>> BindAsync<TResult, TState>(Func<T, TState, ValueTask<Result<TResult>>> binder, TState state)
        {
            var initialResult = await asyncResult.ConfigureAwait(false);

            if (initialResult.TryMatch(out T? success) && success is not null)
            {
                return await binder(success, state).ConfigureAwait(false);
            }

            if (initialResult.TryMatch(out Error failure))
            {
                return Result.Failure<TResult>(failure);
            }

            return Result.FromUninitialized<TResult>();
        }

        public async ValueTask<Result<TResult>> BindAsync<TResult, TState>(Func<T, TState, CancellationToken, ValueTask<Result<TResult>>> binder, TState state, CancellationToken cancellationToken)
        {
            var initialResult = await asyncResult.ConfigureAwait(false);

            if (initialResult.TryMatch(out T? success) && success is not null)
            {
                return await binder(success, state, cancellationToken).ConfigureAwait(false);
            }

            if (initialResult.TryMatch(out Error failure))
            {
                return Result.Failure<TResult>(failure);
            }

            return Result.FromUninitialized<TResult>();
        }

        /// <summary>
        /// Asynchronously transforms a successful result into a new result by applying a mapper function.
        /// If the result is a failure, the failure is propagated.
        /// </summary>
        /// <typeparam name="TResult">The type of the success value of the new result.</typeparam>
        /// <param name="mapper">An asynchronous function to apply to the success value.</param>
        /// <returns>An <see cref="ValueTask{TResult}"/> which will complete once the mapping is done.</returns>
        public async ValueTask<Result<TResult>> MapAsync<TResult>(Func<T, ValueTask<TResult>> mapper)
        {
            var res = await asyncResult.ConfigureAwait(false);

            if (res.TryMatch(out T? success) && success is not null)
            {
                var mapped = await mapper(success).ConfigureAwait(false);
                return Result.Success(mapped);
            }

            if (res.TryMatch(out Error error))
            {
                return Result.Failure<TResult>(error);
            }

            return Result.FromUninitialized<TResult>();
        }

        public async ValueTask<Result<TResult>> MapAsync<TResult>(Func<T, CancellationToken, ValueTask<TResult>> mapper, CancellationToken cancellationToken)
        {
            var res = await asyncResult.ConfigureAwait(false);

            if (res.TryMatch(out T? success) && success is not null)
            {
                var mapped = await mapper(success, cancellationToken).ConfigureAwait(false);
                return Result.Success(mapped);
            }

            if (res.TryMatch(out Error error))
            {
                return Result.Failure<TResult>(error);
            }

            return Result.FromUninitialized<TResult>();
        }

        public async ValueTask<Result<TResult>> MapAsync<TResult, TState>(Func<T, TState, ValueTask<TResult>> mapper, TState state)
        {
            var res = await asyncResult.ConfigureAwait(false);

            if (res.TryMatch(out T? success) && success is not null)
            {
                var mapped = await mapper(success, state).ConfigureAwait(false);
                return Result.Success(mapped);
            }

            if (res.TryMatch(out Error error))
            {
                return Result.Failure<TResult>(error);
            }

            return Result.FromUninitialized<TResult>();
        }

        public async ValueTask<Result<TResult>> MapAsync<TResult, TState>(Func<T, TState, CancellationToken, ValueTask<TResult>> mapper, TState state, CancellationToken cancellationToken)
        {
            var res = await asyncResult.ConfigureAwait(false);

            if (res.TryMatch(out T? success) && success is not null)
            {
                var mapped = await mapper(success, state, cancellationToken).ConfigureAwait(false);
                return Result.Success(mapped);
            }

            if (res.TryMatch(out Error error))
            {
                return Result.Failure<TResult>(error);
            }

            return Result.FromUninitialized<TResult>();
        }
    }
}