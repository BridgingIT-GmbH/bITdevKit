// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Enforces per-operation blob-store client timeouts using linked cancellation.
/// </summary>
/// <example>
/// <code>
/// services.AddBlobStorage()
///     .WithTimeoutBehavior(options => options.Timeout = TimeSpan.FromSeconds(10))
///     .WithInMemoryClient("reports");
/// </code>
/// </example>
/// <remarks>
/// Initializes a new instance of the <see cref="TimeoutBlobStoreClientBehavior" /> class.
/// </remarks>
/// <example>
/// <code>
/// var behavior = new TimeoutBlobStoreClientBehavior(inner, options, "reports");
/// </code>
/// </example>
public sealed class TimeoutBlobStoreClientBehavior : BlobStoreClientBehaviorBase
{
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeoutBlobStoreClientBehavior" /> class.
    /// </summary>
    /// <param name="inner">The decorated blob-store client.</param>
    /// <param name="options">The timeout options.</param>
    /// <param name="storeName">The configured blob-store client name.</param>
    /// <param name="timeProvider">The time provider used to schedule deadlines.</param>
    /// <example>
    /// <code>
    /// var behavior = new TimeoutBlobStoreClientBehavior(inner, options, "reports", TimeProvider.System);
    /// </code>
    /// </example>
    public TimeoutBlobStoreClientBehavior(
        IBlobStoreClient inner,
        TimeoutBlobStoreClientBehaviorOptions options = null,
        string storeName = null,
        TimeProvider timeProvider = null)
        : base(inner, storeName)
    {
        this.Options = Normalize(options);
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Gets the timeout options used by this behavior.
    /// </summary>
    /// <example>
    /// <code>
    /// var timeout = behavior.Options.Timeout;
    /// </code>
    /// </example>
    public TimeoutBlobStoreClientBehaviorOptions Options { get; }

    /// <inheritdoc/>
    protected override Task<Result<T>> ExecuteAsync<T>(
        string operation,
        BlobStoreOperationContext context,
        Func<CancellationToken, Task<Result<T>>> next,
        CancellationToken cancellationToken) =>
        this.ExecuteWithTimeoutAsync(operation, next, Result<T>.Failure, cancellationToken, DisposeValueAsync);

    /// <inheritdoc/>
    protected override Task<Result> ExecuteAsync(
        string operation,
        BlobStoreOperationContext context,
        Func<CancellationToken, Task<Result>> next,
        CancellationToken cancellationToken) =>
        this.ExecuteWithTimeoutAsync(operation, next, Result.Failure, cancellationToken);

    private async Task<TResult> ExecuteWithTimeoutAsync<TResult>(
        string operation,
        Func<CancellationToken, Task<TResult>> next,
        Func<BlobStoreTimeoutError, TResult> failure,
        CancellationToken cancellationToken,
        Func<TResult, ValueTask> disposeResult = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var operationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var delaySource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var operationTask = next(operationSource.Token);
        var timeout = this.Options.Timeout;
        var timeoutTask = Task.Delay(timeout, this.timeProvider, delaySource.Token);
        var cancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, delaySource.Token);

        var completed = await Task.WhenAny(operationTask, timeoutTask, cancellationTask).ConfigureAwait(false);
        await delaySource.CancelAsync().ConfigureAwait(false);
        if (completed == operationTask && !cancellationToken.IsCancellationRequested)
        {
            return await operationTask.ConfigureAwait(false);
        }

        if (!operationTask.IsCompleted)
        {
            await operationSource.CancelAsync().ConfigureAwait(false);
        }

        await ObserveCompletionAsync(operationTask).ConfigureAwait(false);
        if (cancellationToken.IsCancellationRequested)
        {
            if (operationTask.IsCompletedSuccessfully && disposeResult is not null)
            {
                try
                {
                    await disposeResult(await operationTask.ConfigureAwait(false)).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    throw new OperationCanceledException("Blob operation canceled while disposing its result.", exception, cancellationToken);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        if (operationTask.IsCompletedSuccessfully)
        {
            return await operationTask.ConfigureAwait(false);
        }

        BlobStoreClientBehaviorTelemetry.IncrementTimeout();
        return failure(new BlobStoreTimeoutError(operation, timeout));
    }

    private static ValueTask DisposeValueAsync<T>(Result<T> result)
    {
        if (result.Value is IAsyncDisposable asyncDisposable)
        {
            return asyncDisposable.DisposeAsync();
        }

        if (result.Value is IDisposable disposable)
        {
            disposable.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    private static TimeoutBlobStoreClientBehaviorOptions Normalize(TimeoutBlobStoreClientBehaviorOptions options)
    {
        options ??= new TimeoutBlobStoreClientBehaviorOptions();
        if (options.Timeout <= TimeSpan.Zero)
        {
            options.Timeout = TimeSpan.FromSeconds(30);
        }

        return options;
    }

    private static async Task ObserveCompletionAsync(Task operationTask)
    {
        try
        {
            await operationTask.ConfigureAwait(false);
        }
        catch
        {
            // Observe faults and wait for the operation to finish before reporting cancellation or timeout.
        }
    }
}
