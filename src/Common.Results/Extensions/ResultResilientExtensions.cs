// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

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
    public static async Task<Result<TValue>> ExecuteAsync<TValue>(
        Func<CancellationToken, Task<TValue>> operation,
        ResultExecuteOptions options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= ResultExecuteOptions.Default;
        var retryCount = 0;

        while (true)
        {
            try
            {
                using var timeoutCts = new CancellationTokenSource();
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    timeoutCts.Token);

                if (options.Timeout.HasValue)
                {
                    timeoutCts.CancelAfter(options.Timeout.Value);
                }

                var value = await operation(linkedCts.Token);
                return Result<TValue>.Success(value);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Result<TValue>.Failure()
                    .WithError(new OperationCancelledError());
            }
            catch (OperationCanceledException)
            {
                return Result<TValue>.Failure()
                    .WithError(new TimeoutError($"Operation timed out after {options.Timeout?.TotalSeconds} seconds."));
            }
            catch (Exception ex)
            {
                if (retryCount < options.MaxRetries &&
                    (options.RetryableException?.Invoke(ex) ?? false))
                {
                    retryCount++;
                    await Task.Delay(options.RetryDelay, cancellationToken);
                    continue;
                }

                return Result<TValue>.Failure()
                    .WithError(new ExceptionError(ex));
            }
        }
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
        var results = new List<TValue>();
        var operationsList = operations.ToList();

        for (var i = 0; i < operationsList.Count; i += batchSize)
        {
            var batch = operationsList
                .Skip(i)
                .Take(batchSize);

            var batchResults = await ExecuteAllAsync(batch, options, cancellationToken);

            if (batchResults.IsFailure)
            {
                return Result<IEnumerable<TValue>>.Failure()
                    .WithErrors(batchResults.Errors)
                    .WithMessages(batchResults.Messages);
            }

            results.AddRange(batchResults.Value);
        }

        return Result<IEnumerable<TValue>>.Success(results);
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
    public static async Task<Result<TValue>> ExecuteWithBackgroundAsync<TValue>(
        Func<CancellationToken, Task<TValue>> operation,
        Func<CancellationToken, Task> backgroundWork,
        ResultExecuteOptions options = null,
        CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var backgroundTask = Task.Run(async () =>
        {
            try
            {
                await backgroundWork(cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Background task cancellation is expected
            }
        }, cts.Token);

        try
        {
            var result = await ExecuteAsync(operation, options, cts.Token);
            cts.Cancel(); // Cancel background work when main operation completes
            await backgroundTask; // Wait for background task to complete
            return result;
        }
        finally
        {
            cts.Cancel(); // Ensure background work is cancelled
            await backgroundTask; // Wait for background task to complete
        }
    }

    /// <summary>
    ///     Processes a sequence of items asynchronously with retry and error handling.
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

        foreach (var item in items)
        {
            var result = await ExecuteAsync(
                async ct => await operation(item, ct),
                options,
                cancellationToken);

            if (result.IsSuccess)
            {
                results.Add(result.Value);
            }
            else
            {
                errors.AddRange(result.Errors);
                messages.AddRange(result.Messages);
            }
        }

        return errors.Any()
            ? Result<IEnumerable<TOutput>>.Failure()
                .WithErrors(errors)
                .WithMessages(messages)
            : Result<IEnumerable<TOutput>>.Success(results);
    }

    /// <summary>
    ///     Executes an operation with retry logic according to the specified policy.
    /// </summary>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="policy">The retry policy to apply.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    /// <example>
    /// <code>
    /// var policy = RetryPolicy.Create()
    ///     .RetryOn<HttpRequestException>()
    ///     .RetryOn<TimeoutException>()
    ///     .RetryWhen(ex => ex.Message.Contains("retry"))
    ///     .WithMaxRetries(3)
    ///     .WithInitialDelay(TimeSpan.FromSeconds(1))
    ///     .WithBackoffMultiplier(2)
    ///     .OnRetry(context => _logger.LogWarning(
    ///         $"Retry attempt {context.Attempt} after error: {context.Exception.Message}"));
    ///
    /// var result = await Result
    ///     .FromAsync(async () => await UpdateDataAsync())
    ///     .WithRetryPolicy(policy);
    /// </code>
    /// </example>
    public static async Task<Result> WithRetryPolicy(
        this Task<Result> operation,
        RetryPolicy policy,
        CancellationToken cancellationToken = default)
    {
        policy ??= new RetryPolicy();
        var attempt = 0;
        var exceptions = new List<Exception>();

        while (true)
        {
            try
            {
                var result = await operation;

                // If the result has errors that are retryable exceptions, handle them
                if (!result.IsSuccess &&
                    result.Errors.OfType<ExceptionError>()
                        .Any(e => policy.ShouldRetry(e.OriginalException)))
                {
                    exceptions.AddRange(result.Errors
                        .OfType<ExceptionError>()
                        .Select(e => e.OriginalException));

                    throw new AggregateException(exceptions);
                }

                return result;
            }
            catch (Exception ex) when (attempt < policy.MaxRetries &&
                                     !cancellationToken.IsCancellationRequested)
            {
                var actualException = ex is AggregateException agg
                    ? agg.InnerExceptions.First()
                    : ex;

                if (!policy.ShouldRetry(actualException))
                {
                    return Result.Failure()
                        .WithError(new ExceptionError(ex))
                        .WithMessage($"Operation failed: {ex.Message}");
                }

                exceptions.Add(actualException);
                attempt++;

                var delay = policy.GetDelayForAttempt(attempt);
                var context = new RetryContext(actualException, attempt, delay);
                policy.OnRetryAction?.Invoke(context);

                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);

                return Result.Failure()
                    .WithError(new ExceptionError(new AggregateException(exceptions)))
                    .WithMessage($"Operation failed after {attempt} retries");
            }
        }
    }

    /// <summary>
    ///     Executes an operation with retry logic according to the specified policy.
    /// </summary>
    /// <typeparam name="TValue">The type of the result value.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="policy">The retry policy to apply.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    /// <example>
    /// <code>
    /// var policy = RetryPolicy.Create()
    ///     .RetryOn<HttpRequestException>()
    ///     .RetryOn<TimeoutException>()
    ///     .RetryWhen(ex => ex.Message.Contains("retry"))
    ///     .WithMaxRetries(3)
    ///     .WithInitialDelay(TimeSpan.FromSeconds(1))
    ///     .WithBackoffMultiplier(2)
    ///     .OnRetry(context => _logger.LogWarning(
    ///         $"Retry attempt {context.Attempt} after error: {context.Exception.Message}"));
    ///
    /// var result = await Result
    ///     .FromAsync(async () => await FetchDataAsync())
    ///     .WithRetryPolicy(policy);
    /// </code>
    /// </example>
    public static async Task<Result<TValue>> WithRetryPolicy<TValue>(
        this Task<Result<TValue>> operation,
        RetryPolicy policy,
        CancellationToken cancellationToken = default)
    {
        policy ??= new RetryPolicy();
        var attempt = 0;
        var exceptions = new List<Exception>();

        while (true)
        {
            try
            {
                var result = await operation;

                // If the result has errors that are retryable exceptions, handle them
                if (!result.IsSuccess &&
                    result.Errors.OfType<ExceptionError>()
                        .Any(e => policy.ShouldRetry(e.OriginalException)))
                {
                    exceptions.AddRange(result.Errors
                        .OfType<ExceptionError>()
                        .Select(e => e.OriginalException));

                    throw new AggregateException(exceptions);
                }

                return result;
            }
            catch (Exception ex) when (attempt < policy.MaxRetries &&
                                     !cancellationToken.IsCancellationRequested)
            {
                var actualException = ex is AggregateException agg
                    ? agg.InnerExceptions.First()
                    : ex;

                if (!policy.ShouldRetry(actualException))
                {
                    return Result<TValue>.Failure()
                        .WithError(new ExceptionError(ex))
                        .WithMessage($"Operation failed: {ex.Message}");
                }

                exceptions.Add(actualException);
                attempt++;

                var delay = policy.GetDelayForAttempt(attempt);
                var context = new RetryContext(actualException, attempt, delay);
                policy.OnRetryAction?.Invoke(context);

                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);

                return Result<TValue>.Failure()
                    .WithError(new ExceptionError(new AggregateException(exceptions)))
                    .WithMessage($"Operation failed after {attempt} retries");
            }
        }
    }
}