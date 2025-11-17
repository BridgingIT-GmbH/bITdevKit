// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

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

    /// <summary>
    ///     Ensures that a condition is met for the contained value within the operation scope.
    /// </summary>
    public ResultOperationScope<T, TOperation> Ensure(Func<T, bool> predicate, IResultError error)
    {
        if (!result.IsSuccess)
        {
            return this;
        }

        return new ResultOperationScope<T, TOperation>(result.Ensure(predicate, error), startAsync)
        {
            operation = this.operation,
            operationStarted = this.operationStarted
        };
    }

    /// <summary>
    ///     Asynchronously ensures that a condition is met for the contained value within the operation scope.
    /// </summary>
    public async Task<ResultOperationScope<T, TOperation>> EnsureAsync(
        Func<T, CancellationToken, Task<bool>> predicate,
        IResultError error,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess)
        {
            return this;
        }

        await this.EnsureOperationStartedAsync(cancellationToken);

        return new ResultOperationScope<T, TOperation>(await result.EnsureAsync(predicate, error, cancellationToken), startAsync)
        {
            operation = this.operation,
            operationStarted = this.operationStarted
        };
    }

    /// <summary>
    ///     Maps a successful Result{T} to a Result{TNew} within the operation scope.
    /// </summary>
    public ResultOperationScope<TNew, TOperation> Map<TNew>(Func<T, TNew> mapper)
    {
        return new ResultOperationScope<TNew, TOperation>(result.Map(mapper), startAsync)
        {
            operation = this.operation,
            operationStarted = this.operationStarted
        };
    }

    /// <summary>
    ///     Asynchronously maps a successful Result{T} to a Result{TNew} within the operation scope.
    /// </summary>
    public async Task<ResultOperationScope<TNew, TOperation>> MapAsync<TNew>(
        Func<T, CancellationToken, Task<TNew>> mapper,
        CancellationToken cancellationToken = default)
    {
        await this.EnsureOperationStartedAsync(cancellationToken);

        return new ResultOperationScope<TNew, TOperation>(await result.MapAsync(mapper, cancellationToken), startAsync)
        {
            operation = this.operation,
            operationStarted = this.operationStarted
        };
    }

    /// <summary>
    ///     Binds a successful Result{T} to another Result{TNew} within the operation scope.
    /// </summary>
    public ResultOperationScope<TNew, TOperation> Bind<TNew>(Func<T, Result<TNew>> binder)
    {
        var newResult = result.Bind(binder);
        return new ResultOperationScope<TNew, TOperation>(newResult, startAsync)
        {
            operation = this.operation,
            operationStarted = this.operationStarted
        };
    }

    /// <summary>
    ///     Asynchronously binds a successful Result{T} to another Result{TNew} within the operation scope.
    /// </summary>
    public async Task<ResultOperationScope<TNew, TOperation>> BindAsync<TNew>(
        Func<T, CancellationToken, Task<Result<TNew>>> binder,
        CancellationToken cancellationToken = default)
    {
        await this.EnsureOperationStartedAsync(cancellationToken);

        return new ResultOperationScope<TNew, TOperation>(await result.BindAsync(binder, cancellationToken), startAsync)
        {
            operation = this.operation,
            operationStarted = this.operationStarted
        };
    }

    /// <summary>
    ///     Executes an action with the current value if the result is successful within the operation scope.
    /// </summary>
    public ResultOperationScope<T, TOperation> Tap(Action<T> operation)
    {
        return new ResultOperationScope<T, TOperation>(result.Tap(operation), startAsync)
        {
            operation = this.operation,
            operationStarted = this.operationStarted
        };
    }

    /// <summary>
    ///     Asynchronously executes an action with the current value if the result is successful within the operation scope.
    /// </summary>
    public async Task<ResultOperationScope<T, TOperation>> TapAsync(
        Func<T, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        await this.EnsureOperationStartedAsync(cancellationToken);

        return new ResultOperationScope<T, TOperation>(await result.TapAsync(operation, cancellationToken), startAsync)
        {
            operation = this.operation,
            operationStarted = this.operationStarted
        };
    }

    /// <summary>
    ///     Asynchronously ensures that a condition is NOT met for the contained value within the operation scope.
    ///     If the predicate returns true, the result fails with the provided error.
    /// </summary>
    public async Task<ResultOperationScope<T, TOperation>> UnlessAsync(
        Func<T, CancellationToken, Task<bool>> predicate,
        IResultError error,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess)
        {
            return this;
        }

        await this.EnsureOperationStartedAsync(cancellationToken);

        return new ResultOperationScope<T, TOperation>(await result.UnlessAsync(predicate, error, cancellationToken), startAsync)
        {
            operation = this.operation,
            operationStarted = this.operationStarted
        };
    }

    /// <summary>
    ///     Asynchronously ensures that a condition is NOT met for the contained value within the operation scope.
    ///     If the operation returns a failed Result, the result fails with the errors from the operation.
    /// </summary>
    public async Task<ResultOperationScope<T, TOperation>> UnlessAsync(
        Func<T, CancellationToken, Task<Result>> operation,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess)
        {
            return this;
        }

        await this.EnsureOperationStartedAsync(cancellationToken);

        return new ResultOperationScope<T, TOperation>(await result.UnlessAsync(operation, cancellationToken), startAsync)
        {
            operation = this.operation,
            operationStarted = this.operationStarted
        };
    }

    /// <summary>
    ///     Ends the operation scope by committing on success or rolling back on failure.
    ///     Uses the IOperationScope interface methods for commit and rollback.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The final Result{T}.</returns>
    /// <example>
    /// <code>
    /// await scope.EndOperationAsync(cancellationToken); // Simplified API
    /// </code>
    /// </example>
    public async Task<Result<T>> EndOperationAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await this.EnsureOperationStartedAsync(cancellationToken);

            if (result.IsSuccess)
            {
                await this.operation.CommitAsync(cancellationToken);
            }
            else
            {
                await this.operation.RollbackAsync(cancellationToken);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            return Result<T>.Failure()
                .WithError(new OperationCancelledError("Operation was cancelled"));
        }
        catch (Exception ex)
        {
            if (this.operation is not null)
            {
                try
                {
                    await this.operation.RollbackAsync(cancellationToken);
                }
                catch
                {
                    // Swallow rollback exceptions to preserve the original exception
                }
            }

            return Result<T>.Failure()
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    ///     Ends the operation scope by committing on success or rolling back on failure.
    ///     This overload allows custom commit/rollback logic via delegates.
    /// </summary>
    /// <param name="commitAsync">The function to commit the operation.</param>
    /// <param name="rollbackAsync">The optional function to rollback the operation on failure or exception.</param>
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
        Func<TOperation, CancellationToken, Task> commitAsync,
        Func<TOperation, Exception, CancellationToken, Task> rollbackAsync = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(commitAsync);

        try
        {
            await this.EnsureOperationStartedAsync(cancellationToken);

            if (result.IsSuccess)
            {
                await commitAsync(this.operation, cancellationToken);
            }
            else if (rollbackAsync is not null)
            {
                await rollbackAsync(this.operation, null, cancellationToken);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            return Result<T>.Failure()
                .WithError(new OperationCancelledError("Operation was cancelled"));
        }
        catch (Exception ex)
        {
            if (this.operation is not null && rollbackAsync is not null)
            {
                try
                {
                    await rollbackAsync(this.operation, ex, cancellationToken);
                }
                catch
                {
                    // Swallow rollback exceptions to preserve the original exception
                }
            }

            return Result<T>.Failure()
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    ///     Ensures the operation has been started, starting it if necessary.
    /// </summary>
    private async Task EnsureOperationStartedAsync(CancellationToken cancellationToken)
    {
        if (!this.operationStarted && result.IsSuccess)
        {
            this.operation = await startAsync(cancellationToken);
            this.operationStarted = true;
        }
    }
}
