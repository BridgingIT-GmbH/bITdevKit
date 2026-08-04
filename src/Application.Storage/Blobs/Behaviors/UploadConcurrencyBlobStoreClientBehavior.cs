// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Applies bounded process-local admission to upload operations while passing other operations through.
/// </summary>
/// <example>
/// <code>
/// services.AddBlobStorage()
///     .WithUploadConcurrencyBehavior()
///     .WithInMemoryClient("reports");
/// </code>
/// </example>
/// <remarks>
/// Initializes a new upload concurrency behavior.
/// </remarks>
/// <param name="inner">The decorated blob-store client.</param>
/// <param name="coordinator">The shared process-local admission coordinator.</param>
/// <param name="options">The validated upload-admission options.</param>
/// <param name="loggerFactory">The optional logger factory.</param>
/// <param name="storeName">The configured named store.</param>
public sealed partial class UploadConcurrencyBlobStoreClientBehavior
    : BlobStoreClientBehaviorBase
{
    private readonly IBlobUploadAdmissionCoordinator coordinator;
    private readonly UploadConcurrencyBlobStoreClientBehaviorOptions options;
    private readonly ILogger logger;

    /// <summary>
    /// Initializes a new upload concurrency behavior.
    /// </summary>
    /// <param name="inner">The decorated blob-store client.</param>
    /// <param name="coordinator">The shared process-local admission coordinator.</param>
    /// <param name="options">The validated upload-admission options.</param>
    /// <param name="loggerFactory">The optional logger factory.</param>
    /// <param name="storeName">The configured named store.</param>
    /// <example>
    /// <code>
    /// var behavior = new UploadConcurrencyBlobStoreClientBehavior(inner, coordinator, options, storeName: "reports");
    /// </code>
    /// </example>
    public UploadConcurrencyBlobStoreClientBehavior(
        IBlobStoreClient inner,
        IBlobUploadAdmissionCoordinator coordinator,
        UploadConcurrencyBlobStoreClientBehaviorOptions options,
        ILoggerFactory loggerFactory = null,
        string storeName = null)
        : base(inner, storeName)
    {
        this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.logger = (loggerFactory ?? NullLoggerFactory.Instance)
            .CreateLogger<UploadConcurrencyBlobStoreClientBehavior>();
        this.coordinator.ConfigureStore(this.StoreName, this.options);
        TypedLogger.LogConfigured(
            this.logger,
            this.StoreName,
            this.options.MaxConcurrentUploads,
            this.options.MaxQueuedUploads,
            this.options.QueueWaitTimeout.TotalMilliseconds);
    }

    protected override async Task<Result<T>> ExecuteAsync<T>(
        string operation,
        BlobStoreOperationContext context,
        Func<CancellationToken, Task<Result<T>>> next,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(operation, "upload", StringComparison.Ordinal) ||
            context.Upload is null)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();

        BlobUploadAdmissionLease admission;
        try
        {
            admission = await this.coordinator
                .AcquireAsync(this.StoreName, this.options, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            BlobStoreClientBehaviorTelemetry.IncrementAdmissionCancellation();
            TypedLogger.LogQueuedCancellation(this.logger, this.StoreName);
            throw;
        }
        catch (Exception exception)
        {
            TypedLogger.LogCoordinatorFailure(this.logger, this.StoreName, exception);
            throw;
        }

        await using (admission.ConfigureAwait(false))
        {
            if (!admission.IsAcquired)
            {
                if (admission.Error is BlobStoreUploadOverloadedError)
                {
                    BlobStoreClientBehaviorTelemetry.IncrementAdmissionRejection();
                    TypedLogger.LogQueueFull(
                        this.logger,
                        this.StoreName,
                        this.options.MaxConcurrentUploads,
                        this.options.MaxQueuedUploads);
                }
                else if (admission.Error is BlobStoreUploadAdmissionTimeoutError)
                {
                    BlobStoreClientBehaviorTelemetry.IncrementAdmissionTimeout();
                    TypedLogger.LogQueueTimeout(
                        this.logger,
                        this.StoreName,
                        this.options.QueueWaitTimeout.TotalMilliseconds);
                }

                return Result<T>.Failure(admission.Error);
            }

            BlobStoreClientBehaviorTelemetry.RecordAdmission(admission.WaitDuration);
            TypedLogger.LogAdmitted(
                this.logger,
                this.StoreName,
                admission.WaitDuration.TotalMilliseconds);

            return await next(cancellationToken).ConfigureAwait(false);
        }
    }

    protected override Task<Result> ExecuteAsync(
        string operation,
        BlobStoreOperationContext context,
        Func<CancellationToken, Task<Result>> next,
        CancellationToken cancellationToken) =>
        next(cancellationToken);

    private static partial class TypedLogger
    {
        [LoggerMessage(
            0,
            LogLevel.Debug,
            "blob upload admission configured (store={StoreName}, maxConcurrent={MaxConcurrent}, maxQueued={MaxQueued}, timeoutMilliseconds={TimeoutMilliseconds})")]
        public static partial void LogConfigured(
            ILogger logger,
            string storeName,
            int maxConcurrent,
            int maxQueued,
            double timeoutMilliseconds);

        [LoggerMessage(
            1,
            LogLevel.Trace,
            "blob upload admitted (store={StoreName}, waitMilliseconds={WaitMilliseconds})")]
        public static partial void LogAdmitted(
            ILogger logger,
            string storeName,
            double waitMilliseconds);

        [LoggerMessage(
            2,
            LogLevel.Warning,
            "blob upload rejected because admission queue is full (store={StoreName}, maxConcurrent={MaxConcurrent}, maxQueued={MaxQueued})")]
        public static partial void LogQueueFull(
            ILogger logger,
            string storeName,
            int maxConcurrent,
            int maxQueued);

        [LoggerMessage(
            3,
            LogLevel.Warning,
            "blob upload admission wait timed out (store={StoreName}, timeoutMilliseconds={TimeoutMilliseconds})")]
        public static partial void LogQueueTimeout(
            ILogger logger,
            string storeName,
            double timeoutMilliseconds);

        [LoggerMessage(
            4,
            LogLevel.Debug,
            "blob upload admission wait cancelled by upstream token (store={StoreName})")]
        public static partial void LogQueuedCancellation(
            ILogger logger,
            string storeName);

        [LoggerMessage(
            5,
            LogLevel.Error,
            "blob upload admission coordinator failed (store={StoreName})")]
        public static partial void LogCoordinatorFailure(
            ILogger logger,
            string storeName,
            Exception exception);
    }
}
