// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Extension methods for Task{ResultOperationScope{T, TOperation}} to enable fluent async chaining.
/// </summary>
public static class ResultOperationScopeTaskExtensions
{
    /// <summary>
    ///     Executes an action with the current value if the result is successful within the operation scope.
    /// </summary>
    public static async Task<ResultOperationScope<T, TOperation>> Tap<T, TOperation>(
        this Task<ResultOperationScope<T, TOperation>> scopeTask,
        Action<T> operation)
        where TOperation : class
    {
        var scope = await scopeTask;
        return scope.Tap(operation);
    }

    /// <summary>
    ///     Asynchronously executes an action with the current value if the result is successful within the operation scope.
    /// </summary>
    public static async Task<ResultOperationScope<T, TOperation>> TapAsync<T, TOperation>(
        this Task<ResultOperationScope<T, TOperation>> scopeTask,
        Func<T, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
        where TOperation : class
    {
        var scope = await scopeTask;
        return await scope.TapAsync(operation, cancellationToken);
    }

    /// <summary>
    ///     Maps a successful Result{T} to a Result{TNew} within the operation scope.
    /// </summary>
    public static async Task<ResultOperationScope<TNew, TOperation>> Map<T, TNew, TOperation>(
        this Task<ResultOperationScope<T, TOperation>> scopeTask,
        Func<T, TNew> mapper)
        where TOperation : class
    {
        var scope = await scopeTask;
        return scope.Map(mapper);
    }

    /// <summary>
    ///     Asynchronously maps a successful Result{T} to a Result{TNew} within the operation scope.
    /// </summary>
    public static async Task<ResultOperationScope<TNew, TOperation>> MapAsync<T, TNew, TOperation>(
        this Task<ResultOperationScope<T, TOperation>> scopeTask,
        Func<T, CancellationToken, Task<TNew>> mapper,
        CancellationToken cancellationToken = default)
        where TOperation : class
    {
        var scope = await scopeTask;
        return await scope.MapAsync(mapper, cancellationToken);
    }

    /// <summary>
    ///     Binds a successful Result{T} to another Result{TNew} within the operation scope.
    /// </summary>
    public static async Task<ResultOperationScope<TNew, TOperation>> Bind<T, TNew, TOperation>(
        this Task<ResultOperationScope<T, TOperation>> scopeTask,
        Func<T, Result<TNew>> binder)
        where TOperation : class
    {
        var scope = await scopeTask;
        return scope.Bind(binder);
    }

    /// <summary>
    ///     Asynchronously binds a successful Result{T} to another Result{TNew} within the operation scope.
    /// </summary>
    public static async Task<ResultOperationScope<TNew, TOperation>> BindAsync<T, TNew, TOperation>(
        this Task<ResultOperationScope<T, TOperation>> scopeTask,
        Func<T, CancellationToken, Task<Result<TNew>>> binder,
        CancellationToken cancellationToken = default)
        where TOperation : class
    {
        var scope = await scopeTask;
        return await scope.BindAsync(binder, cancellationToken);
    }

    /// <summary>
    ///     Ensures that a condition is met for the contained value within the operation scope.
    /// </summary>
    public static async Task<ResultOperationScope<T, TOperation>> Ensure<T, TOperation>(
        this Task<ResultOperationScope<T, TOperation>> scopeTask,
        Func<T, bool> predicate,
        IResultError error)
        where TOperation : class
    {
        var scope = await scopeTask;
        return scope.Ensure(predicate, error);
    }

    /// <summary>
    ///     Asynchronously ensures that a condition is met for the contained value within the operation scope.
    /// </summary>
    public static async Task<ResultOperationScope<T, TOperation>> EnsureAsync<T, TOperation>(
        this Task<ResultOperationScope<T, TOperation>> scopeTask,
        Func<T, CancellationToken, Task<bool>> predicate,
        IResultError error,
        CancellationToken cancellationToken = default)
        where TOperation : class
    {
        var scope = await scopeTask;
        return await scope.EnsureAsync(predicate, error, cancellationToken);
    }

    /// <summary>
    ///     Asynchronously ensures that a condition is NOT met for the contained value within the operation scope.
    ///     If the predicate returns true, the result fails with the provided error.
    /// </summary>
    public static async Task<ResultOperationScope<T, TOperation>> UnlessAsync<T, TOperation>(
        this Task<ResultOperationScope<T, TOperation>> scopeTask,
        Func<T, CancellationToken, Task<bool>> predicate,
        IResultError error,
        CancellationToken cancellationToken = default)
        where TOperation : class
    {
        var scope = await scopeTask;
        return await scope.UnlessAsync(predicate, error, cancellationToken);
    }

    /// <summary>
    ///     Asynchronously ensures that a condition is NOT met for the contained value within the operation scope.
    ///     If the operation returns a failed Result, the result fails with the errors from the operation.
    /// </summary>
    public static async Task<ResultOperationScope<T, TOperation>> UnlessAsync<T, TOperation>(
        this Task<ResultOperationScope<T, TOperation>> scopeTask,
        Func<T, CancellationToken, Task<Result>> operation,
        CancellationToken cancellationToken = default)
        where TOperation : class
    {
        var scope = await scopeTask;
        return await scope.UnlessAsync(operation, cancellationToken);
    }

    /// <summary>
    ///     Ends the operation scope by committing on success or rolling back on failure.
    /// </summary>
    public static async Task<Result<T>> EndOperationAsync<T, TOperation>(
        this Task<ResultOperationScope<T, TOperation>> scopeTask,
        Func<TOperation, CancellationToken, Task> commitAsync,
        Func<TOperation, Exception, CancellationToken, Task> rollbackAsync = null,
        CancellationToken cancellationToken = default)
        where TOperation : class
    {
        var scope = await scopeTask;
        return await scope.EndOperationAsync(commitAsync, rollbackAsync, cancellationToken);
    }
}
