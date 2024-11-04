// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

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
    public static async Task<Result> ExecuteAsync(
        Func<CancellationToken, Task<Result>> operation,
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

                return await operation(linkedCts.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Result.Failure()
                    .WithError(new OperationCancelledError());
            }
            catch (OperationCanceledException)
            {
                return Result.Failure()
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

                return Result.Failure()
                    .WithError(new ExceptionError(ex));
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
    public static async Task<Result> ExecuteWithBackgroundAsync(
        Func<CancellationToken, Task<Result>> operation,
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
            await cts.CancelAsync(); // Cancel background work when main operation completes
            await backgroundTask; // Wait for background task to complete
            return result;
        }
        catch (Exception)
        {
            await cts.CancelAsync(); // Ensure background work is cancelled on error
            await backgroundTask; // Wait for background task to complete
            throw;
        }
    }
}