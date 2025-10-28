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
/// <typeparam name="TOperation">The type of the operation (e.g., IDbContextTransaction).</typeparam>
/// <example>
/// <code>
/// var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
/// await Result{Unit}.Success()
///     .StartOperation(() => transaction)
///     .EnsureAsync(async (e, ct) => await CheckPermissionsAsync())
///     .BindAsync(async (e, ct) => await repository.DeleteAsync(id))
///     .EndOperationAsync(
///         async (tx, ct) => await tx.CommitAsync(ct),
///         async (tx, ex, ct) => await tx.RollbackAsync(ct),
///         cancellationToken
///     );
/// </code>
/// </example>
public sealed class ResultOperationScope<T, TOperation>
    where TOperation : class
{
    private readonly Result<T> result;
    private readonly Func<CancellationToken, Task<TOperation>> startAsync;
    private TOperation operation;
    private bool operationStarted;

    internal ResultOperationScope(Result<T> result, Func<CancellationToken, Task<TOperation>> startAsync)
    {
        this.result = result;
        this.startAsync = startAsync;
    }

    /// <summary>
    ///     Ensures that a condition is met for the contained value within the operation scope.
    /// </summary>
    public ResultOperationScope<T, TOperation> Ensure(Func<T, bool> predicate, IResultError error)
    {
        if (!this.result.IsSuccess)
        {
            return this;
        }

        var newResult = this.result.Ensure(predicate, error);
        return new ResultOperationScope<T, TOperation>(newResult, this.startAsync)
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
        if (!this.result.IsSuccess)
        {
            return this;
        }

        await this.EnsureOperationStartedAsync(cancellationToken);

        var newResult = await this.result.EnsureAsync(predicate, error, cancellationToken);
        return new ResultOperationScope<T, TOperation>(newResult, this.startAsync)
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
        var newResult = this.result.Map(mapper);
        return new ResultOperationScope<TNew, TOperation>(newResult, this.startAsync)
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

        var newResult = await this.result.MapAsync(mapper, cancellationToken);
        return new ResultOperationScope<TNew, TOperation>(newResult, this.startAsync)
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
        var newResult = this.result.Bind(binder);
        return new ResultOperationScope<TNew, TOperation>(newResult, this.startAsync)
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

        var newResult = await this.result.BindAsync(binder, cancellationToken);
        return new ResultOperationScope<TNew, TOperation>(newResult, this.startAsync)
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
        var newResult = this.result.Tap(operation);
        return new ResultOperationScope<T, TOperation>(newResult, this.startAsync)
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

        var newResult = await this.result.TapAsync(operation, cancellationToken);
        return new ResultOperationScope<T, TOperation>(newResult, this.startAsync)
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
        if (!this.result.IsSuccess)
        {
            return this;
        }

        await this.EnsureOperationStartedAsync(cancellationToken);

        var newResult = await this.result.UnlessAsync(predicate, error, cancellationToken);
        return new ResultOperationScope<T, TOperation>(newResult, this.startAsync)
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
        if (!this.result.IsSuccess)
        {
            return this;
        }

        await this.EnsureOperationStartedAsync(cancellationToken);

        var newResult = await this.result.UnlessAsync(operation, cancellationToken);
        return new ResultOperationScope<T, TOperation>(newResult, this.startAsync)
        {
            operation = this.operation,
            operationStarted = this.operationStarted
        };
    }

    /// <summary>
    ///     Ends the operation scope by committing on success or rolling back on failure.
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
        if (commitAsync is null)
        {
            throw new ArgumentNullException(nameof(commitAsync));
        }

        try
        {
            await this.EnsureOperationStartedAsync(cancellationToken);

            if (this.result.IsSuccess)
            {
                await commitAsync(this.operation, cancellationToken);
            }
            else if (rollbackAsync is not null)
            {
                await rollbackAsync(this.operation, null, cancellationToken);
            }

            return this.result;
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
                .WithErrors(this.result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.result.Messages);
        }
    }

    /// <summary>
    ///     Ensures the operation has been started, starting it if necessary.
    /// </summary>
    private async Task EnsureOperationStartedAsync(CancellationToken cancellationToken)
    {
        if (!this.operationStarted && this.result.IsSuccess)
        {
            this.operation = await this.startAsync(cancellationToken);
            this.operationStarted = true;
        }
    }
}
