// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Provides timeout, retry, batching, cancellation, and background-work execution for value results.</summary>
public static class ResultResilientExtensions
{
    /// <summary>
    ///     Executes an async operation with timeout, retry, and cancellation support.
    /// </summary>
    /// <typeparam name="TValue">The type of the result value.</typeparam>
    /// <param name="operation">The async operation to execute.</param>
    /// <param name="options">Optional configuration for the async operation.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A Result containing the operation outcome.</returns>
    /// <example>
    /// <code>
    /// var result = await Result{User}.ExecuteAsync(
    ///     async ct => await _userService.GetUserAsync(userId, ct),
    ///     AsyncOptions.Default,
    ///     cancellationToken
    /// );
    /// </code>
    /// </example>
    public static Task<Result<TValue>> ExecuteAsync<TValue>(
        Func<CancellationToken, Task<TValue>> operation,
        ResultExecuteOptions options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return ResultAsyncExtensions.ExecuteCoreAsync(
            async ct => Result<TValue>.Success(await operation(ct).AnyContext()),
            static error => Result<TValue>.Failure(error),
            options,
            cancellationToken);
    }

    /// <summary>
    ///     Executes multiple async operations in parallel with timeout and cancellation support.
    /// </summary>
    /// <typeparam name="TValue">The type of the result values.</typeparam>
    /// <param name="operations">The collection of async operations to execute.</param>
    /// <param name="options">Optional configuration for the async operations.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A Result containing all operation results.</returns>
    /// <example>
    /// <code>
    /// var operations = userIds.Select(id =>
    ///     async ct => await _userService.GetUserAsync(id, ct)
    /// );
    /// var result = await Result{User}.ExecuteAllAsync(operations);
    /// </code>
    /// </example>
    public static async Task<Result<IEnumerable<TValue>>> ExecuteAllAsync<TValue>(
        IEnumerable<Func<CancellationToken, Task<TValue>>> operations,
        ResultExecuteOptions options = null,
        CancellationToken cancellationToken = default)
    {
        var tasks = operations.Select(op =>
            ExecuteAsync(op, options, cancellationToken));

        var results = await Task.WhenAll(tasks);
        if (results.Any(r => r.IsFailure))
        {
            return Result<IEnumerable<TValue>>.Failure()
                .WithErrors(results.SelectMany(r => r.Errors))
                .WithMessages(results.SelectMany(r => r.Messages));
        }

        return Result<IEnumerable<TValue>>.Success(results.Select(r => r.Value))
            .WithMessages(results.SelectMany(r => r.Messages));
    }

    /// <summary>
    ///     Executes multiple async operations in parallel with batching support.
    ///     Reads one batch at a time and stops reading the source when the caller cancels.
    /// </summary>
    /// <typeparam name="TValue">The type of the result values.</typeparam>
    /// <param name="operations">The collection of async operations to execute.</param>
    /// <param name="batchSize">The maximum number of concurrent operations.</param>
    /// <param name="options">Optional configuration for the async operations.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A Result containing all operation results.</returns>
    /// <example>
    /// <code>
    /// var operations = userIds.Select(id =>
    ///     async ct => await _userService.GetUserAsync(id, ct)
    /// );
    /// var result = await Result{User}.ExecuteAllBatchedAsync(operations, batchSize: 10);
    /// </code>
    /// </example>
    public static async Task<Result<IEnumerable<TValue>>> ExecuteAllBatchedAsync<TValue>(
        IEnumerable<Func<CancellationToken, Task<TValue>>> operations,
        int batchSize,
        ResultExecuteOptions options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        ArgumentNullException.ThrowIfNull(operations);
        var results = new List<TValue>();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var source = operations.GetEnumerator();
            var batch = new List<Func<CancellationToken, Task<TValue>>>();
            bool hasNext;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                hasNext = source.MoveNext();
                cancellationToken.ThrowIfCancellationRequested();

                if (hasNext)
                {
                    batch.Add(source.Current);
                    if (batch.Count < batchSize)
                    {
                        continue;
                    }
                }
                else if (batch.Count == 0)
                {
                    break;
                }

                var batchResults = await ExecuteAllAsync(batch, options, cancellationToken).AnyContext();
                if (batchResults.IsFailure)
                {
                    return batchResults;
                }

                results.AddRange(batchResults.Value);
                batch.Clear();
            }
            while (hasNext);

            cancellationToken.ThrowIfCancellationRequested();
            return Result<IEnumerable<TValue>>.Success(results);
        }
        catch (OperationCanceledException)
        {
            return Result<IEnumerable<TValue>>.Failure(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<TValue>>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Executes an async operation with a background task that can be cancelled.
    /// </summary>
    /// <typeparam name="TValue">The type of the result value.</typeparam>
    /// <param name="operation">The main async operation.</param>
    /// <param name="backgroundWork">The background work to perform during the operation.</param>
    /// <param name="options">Optional configuration for the async operation.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A Result containing the operation outcome.</returns>
    /// <example>
    /// <code>
    /// var result = await Result{FileData}.ExecuteWithBackgroundAsync(
    ///     async ct => await ProcessLargeFileAsync(fileId, ct),
    ///     async ct => await UpdateProgressIndicatorAsync(ct)
    /// );
    /// </code>
    /// </example>
    public static Task<Result<TValue>> ExecuteWithBackgroundAsync<TValue>(
        Func<CancellationToken, Task<TValue>> operation,
        Func<CancellationToken, Task> backgroundWork,
        ResultExecuteOptions options = null,
        CancellationToken cancellationToken = default) =>
        ResultAsyncExtensions.ExecuteWithBackgroundCoreAsync(
            ct => ExecuteAsync(operation, options, ct), backgroundWork, cancellationToken);

    /// <summary>
    ///     Processes a sequence of items asynchronously with retry and error handling.
    ///     Caller cancellation stops reading the source and preserves diagnostics already collected.
    /// </summary>
    /// <typeparam name="TInput">The type of input items.</typeparam>
    /// <typeparam name="TOutput">The type of output items.</typeparam>
    /// <param name="items">The items to process.</param>
    /// <param name="operation">The async operation to perform on each item.</param>
    /// <param name="options">Optional configuration for the async operations.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A Result containing the processed items.</returns>
    /// <example>
    /// <code>
    /// var items = await orderIds.ProcessSequenceAsync(
    ///     async (id, ct) => await _orderService.ProcessOrderAsync(id, ct)
    /// );
    /// </code>
    /// </example>
    public static async Task<Result<IEnumerable<TOutput>>> ProcessSequenceAsync<TInput, TOutput>(
        this IEnumerable<TInput> items,
        Func<TInput, CancellationToken, Task<TOutput>> operation,
        ResultExecuteOptions options = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<TOutput>();
        var errors = new List<IResultError>();
        var messages = new List<string>();
        var hasFailure = false;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var source = items.GetEnumerator();
            while (!cancellationToken.IsCancellationRequested && source.MoveNext())
            {
                var item = source.Current;
                var result = await ExecuteAsync(
                    ct => operation(item, ct),
                    options,
                    cancellationToken).AnyContext();

                if (result.IsSuccess)
                {
                    results.Add(result.Value);
                }
                else
                {
                    hasFailure = true;
                    errors.AddRange(result.Errors);
                    messages.AddRange(result.Messages);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            hasFailure = true;
            if (!errors.Any(static error => error is OperationCancelledError))
            {
                errors.Add(new OperationCancelledError());
            }
        }

        return hasFailure
            ? Result<IEnumerable<TOutput>>.Failure()
                .WithErrors(errors)
                .WithMessages(messages)
            : Result<IEnumerable<TOutput>>.Success(results);
    }

    /// <summary>Observes an existing task once and converts thrown errors to a result.</summary>
    /// <param name="operation">The existing task.</param>
    /// <param name="policy">The legacy policy parameter.</param>
    /// <param name="cancellationToken">The legacy cancellation parameter.</param>
    /// <returns>The observed outcome.</returns>
    [Obsolete("Use the overload that takes an operation factory so each retry can start a new attempt.")]
    public static Task<Result> WithRetryPolicy(
        this Task<Result> operation,
        RetryPolicy policy,
        CancellationToken cancellationToken = default) =>
        ObserveAsync(operation, static error => Result.Failure(error));

    /// <summary>Observes an existing value result task once and converts thrown errors to a result.</summary>
    /// <typeparam name="TValue">The type of the result value.</typeparam>
    /// <param name="operation">The existing task.</param>
    /// <param name="policy">The legacy policy parameter.</param>
    /// <param name="cancellationToken">The legacy cancellation parameter.</param>
    /// <returns>The observed outcome.</returns>
    [Obsolete("Use the overload that takes an operation factory so each retry can start a new attempt.")]
    public static Task<Result<TValue>> WithRetryPolicy<TValue>(
        this Task<Result<TValue>> operation,
        RetryPolicy policy,
        CancellationToken cancellationToken = default) =>
        ObserveAsync(operation, static error => Result<TValue>.Failure(error));

    /// <summary>Runs an operation factory again for each retry allowed by the policy.</summary>
    /// <param name="operation">The factory invoked on every attempt.</param>
    /// <param name="policy">The policy used to match exceptions and schedule retries.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>The final result, with cancellation represented as an <see cref="OperationCancelledError"/>.</returns>
    /// <example>
    /// <code>
    /// Func&lt;CancellationToken, Task&lt;Result&gt;&gt; save = UpdateOrderAsync;
    /// var result = await save.WithRetryPolicy(RetryPolicy.Create().RetryOn&lt;TimeoutException&gt;());
    /// </code>
    /// </example>
    public static Task<Result> WithRetryPolicy(
        this Func<CancellationToken, Task<Result>> operation,
        RetryPolicy policy,
        CancellationToken cancellationToken = default) =>
        ExecuteWithRetryAsync(operation, static (result, error) => result.WithError(error), policy, cancellationToken);

    /// <summary>Runs an operation factory again for each retry allowed by the policy.</summary>
    /// <typeparam name="TValue">The type of the result value.</typeparam>
    /// <param name="operation">The factory invoked on every attempt.</param>
    /// <param name="policy">The policy used to match exceptions and schedule retries.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>The final result, with cancellation represented as an <see cref="OperationCancelledError"/>.</returns>
    /// <example>
    /// <code>
    /// Func&lt;CancellationToken, Task&lt;Result&lt;Order&gt;&gt;&gt; fetch = LoadOrderAsync;
    /// var result = await fetch.WithRetryPolicy(RetryPolicy.Create().RetryOn&lt;TimeoutException&gt;());
    /// </code>
    /// </example>
    public static Task<Result<TValue>> WithRetryPolicy<TValue>(
        this Func<CancellationToken, Task<Result<TValue>>> operation,
        RetryPolicy policy,
        CancellationToken cancellationToken = default) =>
        ExecuteWithRetryAsync(operation, static (result, error) => result.WithError(error), policy, cancellationToken);

    private static async Task<TResult> ExecuteWithRetryAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        Func<TResult, IResultError, TResult> withError,
        RetryPolicy policy,
        CancellationToken cancellationToken)
        where TResult : struct, IResult
    {
        ArgumentNullException.ThrowIfNull(operation);
        policy ??= RetryPolicy.Create();

        for (var retries = 0; ; retries++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return withError(default, new OperationCancelledError());
            }

            TResult result = default;
            Exception operationException = null;
            try
            {
                result = await operation(cancellationToken).AnyContext();
            }
            catch (OperationCanceledException)
            {
                return withError(default, new OperationCancelledError());
            }
            catch (Exception ex)
            {
                operationException = ex;
            }

            if (result.IsSuccess || retries >= policy.MaxRetries || result.HasError<OperationCancelledError>())
            {
                return CompleteResult();
            }

            var exception = operationException is not null
                ? FindRetryableException(operationException, policy)
                : result.Errors.OfType<ExceptionError>()
                    .Select(error => FindRetryableException(error.Exception, policy))
                    .FirstOrDefault(match => match is not null);
            if (exception is null)
            {
                return CompleteResult();
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var attempt = retries + 1;
                var delay = policy.GetDelayForAttempt(attempt);
                policy.OnRetryAction?.Invoke(new RetryContext(exception, attempt, delay));
                await Task.Delay(delay, cancellationToken).AnyContext();
            }
            catch (OperationCanceledException)
            {
                return withError(CompleteResult(), new OperationCancelledError());
            }

            TResult CompleteResult() => operationException is null
                ? result
                : withError(result, Result.Settings.ExceptionErrorFactory(operationException));
        }
    }

    private static Exception FindRetryableException(Exception exception, RetryPolicy policy)
    {
        if (exception is null or OperationCanceledException)
        {
            return null;
        }

        if (policy.ShouldRetry(exception))
        {
            return exception;
        }

        return exception is AggregateException aggregate
            ? aggregate.InnerExceptions
                .Select(inner => FindRetryableException(inner, policy))
                .FirstOrDefault(match => match is not null)
            : null;
    }

    private static async Task<TResult> ObserveAsync<TResult>(Task<TResult> task, Func<IResultError, TResult> failure)
    {
        ArgumentNullException.ThrowIfNull(task);

        try
        {
            return await task.AnyContext();
        }
        catch (OperationCanceledException)
        {
            return failure(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return failure(Result.Settings.ExceptionErrorFactory(ex));
        }
    }
}
