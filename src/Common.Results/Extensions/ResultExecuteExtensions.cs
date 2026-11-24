// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Provides resilient asynchronous execution helpers that represent operational failures as <see cref="Result"/> values.</summary>
public static class ResultAsyncExtensions
{
    /// <summary>
    ///     Executes an async operation with timeout, retry, and cancellation support.
    /// </summary>
    /// <param name="operation">The async operation to execute.</param>
    /// <param name="options">Optional configuration for the async operation.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A Result containing the operation outcome.</returns>
    /// <example>
    /// <code>
    /// var result = await Result.ExecuteAsync(
    ///     async ct => {
    ///         await _userService.ValidateUserAsync(userId, ct);
    ///         return Result.Success();
    ///     },
    ///     AsyncOptions.Default,
    ///     cancellationToken
    /// );
    /// </code>
    /// </example>
    public static Task<Result> ExecuteAsync(
        Func<CancellationToken, Task<Result>> operation,
        ResultExecuteOptions options = null,
        CancellationToken cancellationToken = default) =>
        ExecuteCoreAsync(operation, static error => Result.Failure(error), options, cancellationToken);

    internal static async Task<TResult> ExecuteCoreAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        Func<IResultError, TResult> failure,
        ResultExecuteOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        options ??= ResultExecuteOptions.Default;

        for (var retryCount = 0; ; retryCount++)
        {
            var timeout = new CancellationTokenSource();
            var timeoutToken = timeout.Token;

            try
            {
                using (timeout)
                {
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (options.Timeout.HasValue)
                    {
                        timeout.CancelAfter(options.Timeout.Value);
                    }

                    return await operation(linked.Token).AnyContext();
                }
            }
            catch (OperationCanceledException)
            {
                return cancellationToken.IsCancellationRequested || !timeoutToken.IsCancellationRequested
                    ? failure(new OperationCancelledError())
                    : failure(new TimeoutError($"Operation timed out after {options.Timeout?.TotalSeconds} seconds."));
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return failure(new OperationCancelledError());
                }

                if (retryCount >= options.MaxRetries || !(options.RetryableException?.Invoke(ex) ?? false))
                {
                    return failure(Result.Settings.ExceptionErrorFactory(ex));
                }
            }

            try
            {
                await Task.Delay(options.RetryDelay, cancellationToken).AnyContext();
            }
            catch (OperationCanceledException)
            {
                return failure(new OperationCancelledError());
            }
        }
    }

    /// <summary>
    ///     Executes multiple async operations in parallel with timeout and cancellation support.
    /// </summary>
    /// <param name="operations">The collection of async operations to execute.</param>
    /// <param name="options">Optional configuration for the async operations.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A Result containing all operation results.</returns>
    /// <example>
    /// <code>
    /// var operations = userIds.Select(id =>
    ///     async ct => await ValidateUserAsync(id, ct)
    /// );
    /// var result = await Result.ExecuteAllAsync(operations);
    /// </code>
    /// </example>
    public static async Task<Result> ExecuteAllAsync(
        IEnumerable<Func<CancellationToken, Task<Result>>> operations,
        ResultExecuteOptions options = null,
        CancellationToken cancellationToken = default)
    {
        var tasks = operations.Select(op =>
            ExecuteAsync(op, options, cancellationToken));

        var results = await Task.WhenAll(tasks);
        if (results.Any(r => r.IsFailure))
        {
            return Result.Failure()
                .WithErrors(results.SelectMany(r => r.Errors))
                .WithMessages(results.SelectMany(r => r.Messages));
        }

        return Result.Success()
            .WithMessages(results.SelectMany(r => r.Messages));
    }

    /// <summary>
    ///     Executes an async operation with a background task that can be cancelled.
    /// </summary>
    /// <param name="operation">The main async operation.</param>
    /// <param name="backgroundWork">The background work to perform during the operation.</param>
    /// <param name="options">Optional configuration for the async operation.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A Result containing the operation outcome.</returns>
    /// <example>
    /// <code>
    /// var result = await Result.ExecuteWithBackgroundAsync(
    ///     async ct => await ProcessDataAsync(ct),
    ///     async ct => await UpdateProgressIndicatorAsync(ct)
    /// );
    /// </code>
    /// </example>
    public static Task<Result> ExecuteWithBackgroundAsync(
        Func<CancellationToken, Task<Result>> operation,
        Func<CancellationToken, Task> backgroundWork,
        ResultExecuteOptions options = null,
        CancellationToken cancellationToken = default) =>
        ExecuteWithBackgroundCoreAsync(
            ct => ExecuteAsync(operation, options, ct), backgroundWork, cancellationToken);

    internal static async Task<TResult> ExecuteWithBackgroundCoreAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        Func<CancellationToken, Task> backgroundWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(backgroundWork);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var backgroundTask = Task.Run(async () =>
        {
            try
            {
                cancellation.Token.ThrowIfCancellationRequested();
                await backgroundWork(cancellation.Token).AnyContext();
            }
            catch (OperationCanceledException)
            {
                // Background cancellation does not determine the main operation's outcome.
            }
        });

        try
        {
            return await operation(cancellation.Token).AnyContext();
        }
        finally
        {
            try
            {
                await cancellation.CancelAsync().AnyContext();
            }
            finally
            {
                await backgroundTask.AnyContext();
            }
        }
    }
}
