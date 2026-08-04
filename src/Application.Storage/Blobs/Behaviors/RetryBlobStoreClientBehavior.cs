// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Retries transient Result-native blob-store client failures.
/// </summary>
/// <example>
/// <code>
/// services.AddBlobStorage()
///     .WithRetryBehavior(options => options.Attempts = 3)
///     .WithInMemoryClient("reports");
/// </code>
/// </example>
/// <remarks>
/// Initializes a new instance of the <see cref="RetryBlobStoreClientBehavior" /> class.
/// </remarks>
/// <param name="inner">The decorated blob-store client.</param>
/// <param name="options">The retry options.</param>
/// <param name="storeName">The configured blob-store client name.</param>
/// <param name="timeProvider">The optional clock used for retry delays.</param>
/// <example>
/// <code>
/// var behavior = new RetryBlobStoreClientBehavior(inner, options, "reports");
/// </code>
/// </example>
public sealed class RetryBlobStoreClientBehavior(
    IBlobStoreClient inner,
    RetryBlobStoreClientBehaviorOptions options = null,
    string storeName = null,
    TimeProvider timeProvider = null) : BlobStoreClientBehaviorBase(inner, storeName)
{
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    /// <summary>
    /// Gets the retry options used by this behavior.
    /// </summary>
    /// <example>
    /// <code>
    /// var attempts = behavior.Options.Attempts;
    /// </code>
    /// </example>
    public RetryBlobStoreClientBehaviorOptions Options { get; } = Normalize(options);

    protected override Task<Result<T>> ExecuteAsync<T>(
        string operation,
        BlobStoreOperationContext context,
        Func<CancellationToken, Task<Result<T>>> next,
        CancellationToken cancellationToken) =>
        this.ExecuteWithRetryAsync(operation, context, next, cancellationToken);

    protected override Task<Result> ExecuteAsync(
        string operation,
        BlobStoreOperationContext context,
        Func<CancellationToken, Task<Result>> next,
        CancellationToken cancellationToken) =>
        this.ExecuteWithRetryAsync(operation, context, next, cancellationToken);

    private async Task<Result<T>> ExecuteWithRetryAsync<T>(
        string operation,
        BlobStoreOperationContext context,
        Func<CancellationToken, Task<Result<T>>> next,
        CancellationToken cancellationToken)
    {
        var startPosition = GetUploadStartPosition(context);
        for (var attempt = 1; attempt <= this.Options.Attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await next(cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess || !this.ShouldRetry(result, context, attempt))
            {
                return result;
            }

            await this.PrepareRetryAsync(context, startPosition, attempt, cancellationToken).ConfigureAwait(false);
        }

        return await next(cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result> ExecuteWithRetryAsync(
        string operation,
        BlobStoreOperationContext context,
        Func<CancellationToken, Task<Result>> next,
        CancellationToken cancellationToken)
    {
        var startPosition = GetUploadStartPosition(context);
        for (var attempt = 1; attempt <= this.Options.Attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await next(cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess || !this.ShouldRetry(result, context, attempt))
            {
                return result;
            }

            await this.PrepareRetryAsync(context, startPosition, attempt, cancellationToken).ConfigureAwait(false);
        }

        return await next(cancellationToken).ConfigureAwait(false);
    }

    private bool ShouldRetry(IResult result, BlobStoreOperationContext context, int attempt)
    {
        if (attempt >= this.Options.Attempts)
        {
            return false;
        }

        if (!IsTransient(result))
        {
            return false;
        }

        if (context.Upload?.Content is not null &&
            !context.Upload.Content.CanSeek &&
            !this.Options.AllowNonSeekableUploadRetries)
        {
            return false;
        }

        return true;
    }

    private async Task PrepareRetryAsync(
        BlobStoreOperationContext context,
        long? startPosition,
        int attempt,
        CancellationToken cancellationToken)
    {
        if (context.Upload?.Content is not null && context.Upload.Content.CanSeek && startPosition is not null)
        {
            context.Upload.Content.Position = startPosition.Value;
        }

        BlobStoreClientBehaviorTelemetry.IncrementRetry();

        var delay = this.GetDelay(attempt);
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, this.timeProvider, cancellationToken).ConfigureAwait(false);
        }
    }

    private TimeSpan GetDelay(int attempt)
    {
        if (this.Options.Backoff <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return this.Options.BackoffExponential
            ? TimeSpan.FromMilliseconds(this.Options.Backoff.TotalMilliseconds * Math.Pow(2, attempt - 1))
            : this.Options.Backoff;
    }

    private static bool IsTransient(IResult result)
    {
        if (result.HasError<BlobStoreValidationError>() ||
            result.HasError<BlobStoreNotFoundError>() ||
            result.HasError<BlobStoreConflictError>() ||
            result.HasError<BlobStoreLeaseError>() ||
            result.HasError<BlobStoreSizeLimitExceededError>() ||
            result.HasError<BlobStoreIntegrityError>() ||
            result.HasError<BlobStoreUploadOverloadedError>() ||
            result.HasError<BlobStoreUploadAdmissionTimeoutError>() ||
            result.HasError<BlobStoreQueryTooBroadError>() ||
            result.HasError<BlobStorePageSizeExceededError>() ||
            result.HasError<BlobStoreQueryNotSupportedError>() ||
            result.HasError<BlobStoreInvalidContinuationTokenError>() ||
            result.HasError<OperationCancelledError>())
        {
            return false;
        }

        return result.HasError<BlobStoreProviderError>() || result.HasError<BlobStoreTimeoutError>();
    }

    private static long? GetUploadStartPosition(BlobStoreOperationContext context)
    {
        var content = context.Upload?.Content;
        if (content is null || !content.CanSeek)
        {
            return null;
        }

        return content.Position;
    }

    private static RetryBlobStoreClientBehaviorOptions Normalize(RetryBlobStoreClientBehaviorOptions options)
    {
        options ??= new RetryBlobStoreClientBehaviorOptions();
        if (options.Attempts <= 0)
        {
            options.Attempts = 1;
        }

        return options;
    }
}
