// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

using System.Diagnostics;

/// <summary>Traces blob store client operations using low-cardinality tags.</summary>
/// <param name="inner">The decorated blob store client.</param>
/// <param name="storeName">The configured blob store name.</param>
/// <example><code>var client = new TracingBlobStoreClientBehavior(inner, "reports");</code></example>
public sealed class TracingBlobStoreClientBehavior(IBlobStoreClient inner, string storeName = null)
    : BlobStoreClientBehaviorBase(inner, storeName)
{
    /// <summary>Traces a blob operation that returns a value.</summary>
    /// <example><code>var result = await client.DownloadAsync(key, cancellationToken);</code></example>
    protected override Task<Result<T>> ExecuteAsync<T>(
        string operation,
        BlobStoreOperationContext context,
        Func<CancellationToken, Task<Result<T>>> next,
        CancellationToken cancellationToken) =>
        BlobTelemetry.Spans.HasListeners()
            ? this.ExecuteTracedAsync(operation, next, cancellationToken)
            : next(cancellationToken);

    /// <summary>Traces a blob operation that returns a result without a value.</summary>
    /// <example><code>var result = await client.DeleteAsync(key, cancellationToken: cancellationToken);</code></example>
    protected override Task<Result> ExecuteAsync(
        string operation,
        BlobStoreOperationContext context,
        Func<CancellationToken, Task<Result>> next,
        CancellationToken cancellationToken) =>
        BlobTelemetry.Spans.HasListeners()
            ? this.ExecuteTracedAsync(operation, next, cancellationToken)
            : next(cancellationToken);

    private async Task<TResult> ExecuteTracedAsync<TResult>(
        string operation,
        Func<CancellationToken, Task<TResult>> next,
        CancellationToken cancellationToken)
        where TResult : IResult
    {
        using var activity = BlobTelemetry.Spans.StartActivity($"blobs.{operation}", ActivityKind.Internal);
        if (activity is null)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        activity.SetTag("blobs.operation", operation);
        activity.SetTag("blobs.store", this.StoreName);

        try
        {
            var result = await next(cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                SetError(activity, result.Errors.FirstOrDefault()?.GetType().Name);
            }

            return result;
        }
        catch (Exception exception)
        {
            SetError(activity, exception.GetType().Name);
            throw;
        }
    }

    private static void SetError(Activity activity, string errorType)
    {
        activity.SetTag("error.type", errorType);
        activity.SetStatus(ActivityStatusCode.Error, errorType);
    }
}
