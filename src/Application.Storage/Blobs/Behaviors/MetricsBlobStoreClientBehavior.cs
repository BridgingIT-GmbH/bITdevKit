// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.Diagnostics;
using BridgingIT.DevKit.Common;

/// <summary>
/// Emits low-cardinality blob-store operation metrics.
/// </summary>
/// <example>
/// <code>
/// services.AddBlobStorage()
///     .WithMetricsBehavior()
///     .WithInMemoryClient("reports");
/// </code>
/// </example>
/// <remarks>
/// Initializes a new instance of the <see cref="MetricsBlobStoreClientBehavior" /> class.
/// </remarks>
/// <param name="inner">The decorated blob-store client.</param>
/// <param name="metricsService">The optional shared metrics service.</param>
/// <param name="storeName">The configured blob-store client name.</param>
/// <example>
/// <code>
/// var behavior = new MetricsBlobStoreClientBehavior(inner, metricsService, "reports");
/// </code>
/// </example>
public sealed class MetricsBlobStoreClientBehavior(
    IBlobStoreClient inner,
    IMetricsService metricsService = null,
    string storeName = null) : BlobStoreClientBehaviorBase(inner, storeName)
{
    protected override async Task<Result<T>> ExecuteAsync<T>(
        string operation,
        BlobStoreOperationContext context,
        Func<CancellationToken, Task<Result<T>>> next,
        CancellationToken cancellationToken)
    {
        if (metricsService is null || cancellationToken.IsCancellationRequested)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var started = Stopwatch.GetTimestamp();
        using var telemetry = BlobStoreClientBehaviorTelemetry.Begin();
        this.AddCounter("blobstorage_operations", 1, operation);

        try
        {
            var result = await next(cancellationToken).ConfigureAwait(false);
            this.Record(operation, started, result, GetBytes(result), GetListItemCount(result), telemetry);

            return result;
        }
        catch (OperationCanceledException)
        {
            if (telemetry.AdmissionCancellations > 0)
            {
                this.AddCounter(
                    "blobstorage_upload_admission_cancellations",
                    telemetry.AdmissionCancellations,
                    operation);
            }

            throw;
        }
    }

    protected override async Task<Result> ExecuteAsync(
        string operation,
        BlobStoreOperationContext context,
        Func<CancellationToken, Task<Result>> next,
        CancellationToken cancellationToken)
    {
        if (metricsService is null || cancellationToken.IsCancellationRequested)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var started = Stopwatch.GetTimestamp();
        using var telemetry = BlobStoreClientBehaviorTelemetry.Begin();
        this.AddCounter("blobstorage_operations", 1, operation);

        var result = await next(cancellationToken).ConfigureAwait(false);
        this.Record(operation, started, result, 0, 0, telemetry);

        return result;
    }

    private void Record(
        string operation,
        long started,
        IResult result,
        long bytes,
        long listItemCount,
        BlobStoreClientBehaviorTelemetry.Scope telemetry)
    {
        this.AddHistogram("blobstorage_operation_duration", Stopwatch.GetElapsedTime(started).TotalMilliseconds, operation);

        if (result.IsFailure)
        {
            this.AddCounter("blobstorage_operation_failures", 1, operation);
        }

        if (bytes > 0)
        {
            this.AddCounter("blobstorage_bytes", bytes, operation);
        }

        if (listItemCount > 0)
        {
            this.AddCounter("blobstorage_list_items", listItemCount, operation);
        }

        if (telemetry.Retries > 0)
        {
            this.AddCounter("blobstorage_retries", telemetry.Retries, operation);
        }

        if (telemetry.Timeouts > 0 || result.HasError<BlobStoreTimeoutError>())
        {
            this.AddCounter("blobstorage_timeouts", Math.Max(1, telemetry.Timeouts), operation);
        }

        if (result.HasError<BlobStoreSizeLimitExceededError>())
        {
            this.AddCounter("blobstorage_size_limit_failures", 1, operation);
        }

        if (telemetry.Admissions > 0)
        {
            this.AddCounter("blobstorage_upload_admissions", telemetry.Admissions, operation);
            foreach (var waitMilliseconds in telemetry.AdmissionWaitMilliseconds)
            {
                this.AddHistogram(
                    "blobstorage_upload_admission_wait",
                    waitMilliseconds,
                    operation);
            }
        }

        if (telemetry.AdmissionRejections > 0)
        {
            this.AddCounter(
                "blobstorage_upload_admission_rejections",
                telemetry.AdmissionRejections,
                operation);
        }

        if (telemetry.AdmissionTimeouts > 0)
        {
            this.AddCounter(
                "blobstorage_upload_admission_timeouts",
                telemetry.AdmissionTimeouts,
                operation);
        }

        if (telemetry.AdmissionCancellations > 0 &&
            telemetry.Timeouts == 0 &&
            !result.HasError<BlobStoreTimeoutError>())
        {
            this.AddCounter(
                "blobstorage_upload_admission_cancellations",
                telemetry.AdmissionCancellations,
                operation);
        }
    }

    private static long GetBytes<T>(Result<T> result) =>
        result is { IsSuccess: true, Value: BlobInfo info } ? info.Length :
        result is { IsSuccess: true, Value: BlobDownload download } ? download.Info?.Length ?? 0 :
        0;

    private static long GetListItemCount<T>(Result<T> result) =>
        result is { IsSuccess: true, Value: BlobPage page } ? page.Items.Count : 0;

    private void AddCounter(string name, long value, string operation)
    {
        metricsService.AddCounter(name, value, this.Tags(operation));
    }

    private void AddHistogram(string name, double value, string operation)
    {
        metricsService.RecordHistogram(name, value, "ms", this.Tags(operation));
    }

    private MetricTag[] Tags(string operation) =>
    [
        new("operation", operation),
        new("store", this.StoreName)
    ];
}
