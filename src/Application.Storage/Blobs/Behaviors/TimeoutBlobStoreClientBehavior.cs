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
/// <param name="inner">The decorated blob-store client.</param>
/// <param name="options">The timeout options.</param>
/// <param name="storeName">The configured blob-store client name.</param>
/// <param name="timeProvider">The time provider used to schedule deadlines.</param>
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
        this.ExecuteWithTimeoutAsync(operation, next, cancellationToken);

    /// <inheritdoc/>
    protected override Task<Result> ExecuteAsync(
        string operation,
        BlobStoreOperationContext context,
        Func<CancellationToken, Task<Result>> next,
        CancellationToken cancellationToken) =>
        this.ExecuteWithTimeoutAsync(operation, next, cancellationToken);

    private async Task<Result<T>> ExecuteWithTimeoutAsync<T>(
        string operation,
        Func<CancellationToken, Task<Result<T>>> next,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var operationTask = next(timeoutSource.Token);
        var timeoutTask = Task.Delay(this.Options.Timeout, this.timeProvider);
        var callerCancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, this.timeProvider, cancellationToken);

        var completed = await Task.WhenAny(operationTask, timeoutTask, callerCancellationTask).ConfigureAwait(false);
        if (completed == operationTask)
        {
            return await operationTask.ConfigureAwait(false);
        }

        await timeoutSource.CancelAsync().ConfigureAwait(false);
        await ObserveCompletionAsync(operationTask).ConfigureAwait(false);
        if (completed == callerCancellationTask || cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        BlobStoreClientBehaviorTelemetry.IncrementTimeout();

        return Result<T>.Failure(new BlobStoreTimeoutError(operation, this.Options.Timeout));
    }

    private async Task<Result> ExecuteWithTimeoutAsync(
        string operation,
        Func<CancellationToken, Task<Result>> next,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var operationTask = next(timeoutSource.Token);
        var timeoutTask = Task.Delay(this.Options.Timeout, this.timeProvider);
        var callerCancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, this.timeProvider, cancellationToken);

        var completed = await Task.WhenAny(operationTask, timeoutTask, callerCancellationTask).ConfigureAwait(false);
        if (completed == operationTask)
        {
            return await operationTask.ConfigureAwait(false);
        }

        await timeoutSource.CancelAsync().ConfigureAwait(false);
        await ObserveCompletionAsync(operationTask).ConfigureAwait(false);
        if (completed == callerCancellationTask || cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        BlobStoreClientBehaviorTelemetry.IncrementTimeout();

        return Result.Failure(new BlobStoreTimeoutError(operation, this.Options.Timeout));
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
            // The deadline result owns the outcome after cancellation; awaiting here observes and quiesces the operation.
        }
    }
}
