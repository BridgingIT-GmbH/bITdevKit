// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Runtime.ExceptionServices;

/// <summary>
///     Represents a scoped operation context for Result{T} that allows wrapping operations
///     like database transactions around a result chain.
/// </summary>
/// <typeparam name="T">The type of the result value.</typeparam>
/// <typeparam name="TOperation">The type of the operation, must implement IOperationScope.</typeparam>
/// <example>
/// <code>
/// await Result{Unit}.Success()
///     .StartOperation(async ct => await transaction.BeginTransactionAsync(ct))
///     .EnsureAsync(async (e, ct) => await CheckPermissionsAsync())
///     .BindAsync(async (e, ct) => await repository.DeleteAsync(id))
///     .EndOperationAsync(cancellationToken); // Simplified API
/// </code>
/// </example>
public class ResultOperationScope<T, TOperation>(Result<T> result, Func<CancellationToken, Task<TOperation>> startAsync)
    where TOperation : class, IOperationScope
{
    private TOperation operation;
    private bool operationStarted;
    private ExceptionDispatchInfo stepFailure;

    internal ResultOperationScope(Result<T> result, TOperation operation)
        : this(result, _ => Task.FromResult(operation))
    {
        this.operation = operation;
        this.operationStarted = true;
    }

    /// <summary>
    ///     Ensures that a condition is met for the contained value within the operation scope.
    /// </summary>
    public ResultOperationScope<T, TOperation> Ensure(Func<T, bool> predicate, IResultError error)
    {
        if (!result.IsSuccess)
        {
            return this;
        }

        return this.RunStep(() => result.Ensure(predicate, error));
    }

    /// <summary>
    ///     Asynchronously ensures that a condition is met for the contained value within the operation scope.
    /// </summary>
    public Task<ResultOperationScope<T, TOperation>> EnsureAsync(
        Func<T, CancellationToken, Task<bool>> predicate,
        IResultError error,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess)
        {
            return Task.FromResult(this);
        }

        return this.RunStepAsync(started => started.EnsureAsync(predicate, error, cancellationToken), cancellationToken);
    }

    /// <summary>
    ///     Maps a successful Result{T} to a Result{TNew} within the operation scope.
    /// </summary>
    public ResultOperationScope<TNew, TOperation> Map<TNew>(Func<T, TNew> mapper)
    {
        return this.RunStep(() => result.Map(mapper));
    }

    /// <summary>
    ///     Asynchronously maps a successful Result{T} to a Result{TNew} within the operation scope.
    /// </summary>
    public Task<ResultOperationScope<TNew, TOperation>> MapAsync<TNew>(
        Func<T, CancellationToken, Task<TNew>> mapper,
        CancellationToken cancellationToken = default)
    {
        return this.RunStepAsync(started => started.MapAsync(mapper, cancellationToken), cancellationToken);
    }

    /// <summary>
    ///     Binds a successful Result{T} to another Result{TNew} within the operation scope.
    /// </summary>
    public ResultOperationScope<TNew, TOperation> Bind<TNew>(Func<T, Result<TNew>> binder)
    {
        return this.RunStep(() => result.Bind(binder));
    }

    /// <summary>
    ///     Asynchronously binds a successful Result{T} to another Result{TNew} within the operation scope.
    /// </summary>
    public Task<ResultOperationScope<TNew, TOperation>> BindAsync<TNew>(
        Func<T, CancellationToken, Task<Result<TNew>>> binder,
        CancellationToken cancellationToken = default)
    {
        return this.RunStepAsync(started => started.BindAsync(binder, cancellationToken), cancellationToken);
    }

    /// <summary>
    ///     Executes an action with the current value if the result is successful within the operation scope.
    /// </summary>
    public ResultOperationScope<T, TOperation> Tap(Action<T> operation)
    {
        return this.RunStep(() => result.Tap(operation));
    }

    /// <summary>
    ///     Asynchronously executes an action with the current value if the result is successful within the operation scope.
    /// </summary>
    public Task<ResultOperationScope<T, TOperation>> TapAsync(
        Func<T, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        return this.RunStepAsync(started => started.TapAsync(operation, cancellationToken), cancellationToken);
    }

    /// <summary>
    ///     Asynchronously ensures that a condition is NOT met for the contained value within the operation scope.
    ///     If the predicate returns true, the result fails with the provided error.
    /// </summary>
    public Task<ResultOperationScope<T, TOperation>> UnlessAsync(
        Func<T, CancellationToken, Task<bool>> predicate,
        IResultError error,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess)
        {
            return Task.FromResult(this);
        }

        return this.RunStepAsync(started => started.UnlessAsync(predicate, error, cancellationToken), cancellationToken);
    }

    /// <summary>
    ///     Asynchronously ensures that a condition is NOT met for the contained value within the operation scope.
    ///     If the operation returns a failed Result, the result fails with the errors from the operation.
    /// </summary>
    public Task<ResultOperationScope<T, TOperation>> UnlessAsync(
        Func<T, CancellationToken, Task<Result>> operation,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess)
        {
            return Task.FromResult(this);
        }

        return this.RunStepAsync(started => started.UnlessAsync(operation, cancellationToken), cancellationToken);
    }

    /// <summary>
    ///     Ends the operation scope by committing on success or rolling back on failure.
    ///     Uses the IOperationScope interface methods for commit and rollback.
    ///     Rollback runs after acquisition with a token independent of caller cancellation.
    ///     An exception that escaped a scoped step propagates after rollback.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The final Result{T}.</returns>
    /// <example>
    /// <code>
    /// await scope.EndOperationAsync(cancellationToken); // Simplified API
    /// </code>
    /// </example>
    public Task<Result<T>> EndOperationAsync(CancellationToken cancellationToken = default) =>
        this.EndOperationAsync(
            static (operation, ct) => operation.CommitAsync(ct),
            static (operation, _, ct) => operation.RollbackAsync(ct),
            cancellationToken);

    /// <summary>
    ///     Ends the operation scope by committing on success or rolling back on failure.
    ///     This overload allows custom commit and rollback logic through delegates.
    ///     Rollback receives an independent token so cancellation can still release acquired resources.
    ///     An exception that escaped a scoped step propagates after rollback.
    /// </summary>
    /// <param name="commit">The function to commit the operation.</param>
    /// <param name="rollback">The optional function to rollback the operation on failure or exception.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The final Result{T}.</returns>
    /// <example>
    /// <code>
    /// await scope.EndOperationAsync(
    ///     async (tx, ct) => await tx.CommitAsync(ct),
    ///     async (tx, ex, ct) => await tx.RollbackAsync(ct),
    ///     cancellationToken
    /// );
    /// </code>
    /// </example>
    public async Task<Result<T>> EndOperationAsync(
        Func<TOperation, CancellationToken, Task> commit,
        Func<TOperation, Exception, CancellationToken, Task> rollback = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(commit);

        var outcome = await this.TryStartOperationAsync(cancellationToken).AnyContext();
        if (!this.operationStarted)
        {
            return outcome;
        }

        Exception commitError = null;
        if (outcome.IsSuccess)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await commit(this.operation, cancellationToken).AnyContext();
                return outcome;
            }
            catch (Exception ex)
            {
                commitError = ex;
                outcome = outcome.AsFailure();
            }
        }

        Exception rollbackError = null;
        if (rollback is not null)
        {
            try
            {
                await rollback(this.operation, this.stepFailure?.SourceException ?? commitError, CancellationToken.None).AnyContext();
            }
            catch (Exception ex)
            {
                rollbackError = ex;
            }
        }

        if (this.stepFailure is not null && rollbackError is not null)
        {
            throw new AggregateException(this.stepFailure.SourceException, rollbackError);
        }

        this.stepFailure?.Throw();
        // Diagnostic factories can throw, so resolve them after rollback.
        return outcome.WithError(ToError(commitError)).WithError(ToError(rollbackError));
    }

    private ResultOperationScope<TNew, TOperation> RunStep<TNew>(Func<Result<TNew>> step)
    {
        try
        {
            return this.ContinueWith(step());
        }
        catch (Exception ex) when (this.operationStarted)
        {
            return this.ContinueWith(Result<TNew>.Failure(result), ExceptionDispatchInfo.Capture(ex));
        }
    }

    private async Task<ResultOperationScope<TNew, TOperation>> RunStepAsync<TNew>(
        Func<Result<T>, Task<Result<TNew>>> step,
        CancellationToken cancellationToken)
    {
        var started = await this.TryStartOperationAsync(cancellationToken).AnyContext();
        if (started.IsSuccess && cancellationToken.IsCancellationRequested)
        {
            started = started.WithError(new OperationCancelledError());
        }

        try
        {
            return this.ContinueWith(await step(started).AnyContext());
        }
        catch (Exception ex) when (this.operationStarted)
        {
            return this.ContinueWith(Result<TNew>.Failure(started), ExceptionDispatchInfo.Capture(ex));
        }
    }

    private ResultOperationScope<TNew, TOperation> ContinueWith<TNew>(
        Result<TNew> next,
        ExceptionDispatchInfo failure = null) =>
        new(next, startAsync)
        {
            operation = this.operation,
            operationStarted = this.operationStarted,
            stepFailure = this.stepFailure ?? failure
        };

    private async Task<Result<T>> TryStartOperationAsync(CancellationToken cancellationToken)
    {
        if (this.operationStarted || result.IsFailure)
        {
            return result;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.operation = await startAsync(cancellationToken).AnyContext();
            if (this.operation is null)
            {
                return result.WithError(new Error("Operation scope factory returned null"));
            }

            this.operationStarted = true;
            return result;
        }
        catch (Exception ex)
        {
            return result.AsFailure().WithError(ToError(ex));
        }
    }

    private static IResultError ToError(Exception exception) => exception switch
    {
        null => null,
        OperationCanceledException => new OperationCancelledError("Operation was cancelled"),
        _ => Result.Settings.ExceptionErrorFactory(exception)
    };
}
