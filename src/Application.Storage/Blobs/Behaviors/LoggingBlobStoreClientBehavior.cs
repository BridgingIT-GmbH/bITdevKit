// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Logs blob-store client operations without logging content, continuation tokens, or property values.
/// </summary>
/// <example>
/// <code>
/// services.AddBlobStorage()
///     .WithLoggingBehavior()
///     .WithInMemoryClient("reports");
/// </code>
/// </example>
/// <remarks>
/// Initializes a new instance of the <see cref="LoggingBlobStoreClientBehavior" /> class.
/// </remarks>
/// <param name="loggerFactory">The logger factory.</param>
/// <param name="inner">The decorated blob-store client.</param>
/// <param name="storeName">The configured blob-store client name.</param>
/// <example>
/// <code>
/// var behavior = new LoggingBlobStoreClientBehavior(loggerFactory, inner, "reports");
/// </code>
/// </example>
public sealed class LoggingBlobStoreClientBehavior(
    ILoggerFactory loggerFactory,
    IBlobStoreClient inner,
    string storeName = null) : BlobStoreClientBehaviorBase(inner, storeName)
{
    private readonly ILogger<LoggingBlobStoreClientBehavior> logger = loggerFactory?.CreateLogger<LoggingBlobStoreClientBehavior>() ??
            NullLoggerFactory.Instance.CreateLogger<LoggingBlobStoreClientBehavior>();

    /// <inheritdoc/>
    protected override async Task<Result<T>> ExecuteAsync<T>(
        string operation,
        BlobStoreOperationContext context,
        Func<CancellationToken, Task<Result<T>>> next,
        CancellationToken cancellationToken)
    {
        this.LogStarting(operation, context);
        var result = await next(cancellationToken).ConfigureAwait(false);
        this.LogCompleted(operation, context, result);

        return result;
    }

    /// <inheritdoc/>
    protected override async Task<Result> ExecuteAsync(
        string operation,
        BlobStoreOperationContext context,
        Func<CancellationToken, Task<Result>> next,
        CancellationToken cancellationToken)
    {
        this.LogStarting(operation, context);
        var result = await next(cancellationToken).ConfigureAwait(false);
        this.LogCompleted(operation, context, result);

        return result;
    }

    private void LogStarting(string operation, BlobStoreOperationContext context)
    {
        this.logger.LogDebug(
            "[{LogKey}] blobclient: {Operation} starting (store={Store}, container={Container}, name={Name}, prefix={Prefix}, take={Take}, allowFullScan={AllowFullScan}, hasContinuation={HasContinuation}, propertyCount={PropertyCount})",
            Constants.LogKey,
            operation,
            this.StoreName,
            context.Key?.Container ?? context.Query?.Container,
            context.Key?.Name,
            context.Query?.Prefix,
            context.Query?.Take,
            context.Query?.AllowFullScan,
            !string.IsNullOrWhiteSpace(context.Query?.ContinuationToken),
            context.Upload?.Properties?.Count ?? context.Update?.Properties?.Count ?? 0);
    }

    private void LogCompleted(string operation, BlobStoreOperationContext context, IResult result)
    {
        this.logger.LogDebug(
            "[{LogKey}] blobclient: {Operation} completed (store={Store}, success={Success}, errorTypes={ErrorTypes}, name={Name}, hasContinuation={HasContinuation})",
            Constants.LogKey,
            operation,
            this.StoreName,
            result.IsSuccess,
            GetErrorTypes(result),
            context.Key?.Name,
            !string.IsNullOrWhiteSpace(context.Query?.ContinuationToken));
    }

    private static string GetErrorTypes(IResult result) =>
        result.Errors.Count == 0
            ? string.Empty
            : string.Join(",", result.Errors.Select(error => error.GetType().Name).Distinct(StringComparer.Ordinal));
}
