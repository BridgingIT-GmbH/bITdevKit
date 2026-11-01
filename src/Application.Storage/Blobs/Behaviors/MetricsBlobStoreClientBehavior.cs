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
    /// <inheritdoc/>
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

            return this.RecordThroughput(operation, started, result);
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

    /// <inheritdoc/>
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

        if (result.IsSuccess && operation is "upload" or "download")
        {
            this.AddCounter("blobstorage_items", 1, operation);
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

    // Uploads and in-memory downloads are timed until the call returns. A streaming download is timed from the
    // start of the call plus the time spent inside its reads, so a slow consumer does not lower the rate.
    private Result<T> RecordThroughput<T>(string operation, long started, Result<T> result)
    {
        if (result.IsFailure)
        {
            return result;
        }

        if (operation == "upload" && result.Value is BlobInfo info)
        {
            this.RecordBytesPerSecond(operation, info.Length, Stopwatch.GetElapsedTime(started));
        }
        else if (operation == "download" && result.Value is BlobDownload { Content: not null } download)
        {
            if (download.Content is MemoryStream memory)
            {
                this.RecordBytesPerSecond(operation, memory.Length, Stopwatch.GetElapsedTime(started));

                return result;
            }

            var metered = new BlobDownload
            {
                Content = new MeteredReadStream(
                    download.Content,
                    Stopwatch.GetElapsedTime(started),
                    (bytes, elapsed) => this.RecordBytesPerSecond(operation, bytes, elapsed)),
                Info = download.Info
            };

            return (Result<T>)(object)Result<BlobDownload>.Success(metered);
        }

        return result;
    }

    private void RecordBytesPerSecond(string operation, long bytes, TimeSpan elapsed)
    {
        if (bytes > 0 && elapsed > TimeSpan.Zero)
        {
            metricsService.RecordHistogram(
                "blobstorage_bytes_per_second",
                bytes / elapsed.TotalSeconds,
                "By/s",
                this.Tags(operation));
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

    // Counts the bytes actually read from a streaming download and records the rate once, when the stream is disposed.
    private sealed class MeteredReadStream(Stream inner, TimeSpan opened, Action<long, TimeSpan> record) : Stream
    {
        private long bytes;
        private long readTicks;
        private int recorded;

        public override bool CanRead => inner.CanRead;

        public override bool CanSeek => inner.CanSeek;

        public override bool CanWrite => false;

        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override void Flush() => inner.Flush();

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) =>
            this.Track(() => inner.Read(buffer, offset, count));

        public override int Read(Span<byte> buffer)
        {
            var started = Stopwatch.GetTimestamp();
            var read = inner.Read(buffer);
            this.Add(read, started);

            return read;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            this.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var started = Stopwatch.GetTimestamp();
            var read = await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            this.Add(read, started);

            return read;
        }

        public override async ValueTask DisposeAsync()
        {
            this.Complete();
            await inner.DisposeAsync().ConfigureAwait(false);
            await base.DisposeAsync().ConfigureAwait(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.Complete();
                inner.Dispose();
            }

            base.Dispose(disposing);
        }

        private int Track(Func<int> read)
        {
            var started = Stopwatch.GetTimestamp();
            var count = read();
            this.Add(count, started);

            return count;
        }

        private void Add(int count, long started)
        {
            Interlocked.Add(ref this.bytes, count);
            Interlocked.Add(ref this.readTicks, Stopwatch.GetTimestamp() - started);
        }

        private void Complete()
        {
            if (Interlocked.Exchange(ref this.recorded, 1) == 0)
            {
                var reading = TimeSpan.FromSeconds((double)Interlocked.Read(ref this.readTicks) / Stopwatch.Frequency);
                record(Interlocked.Read(ref this.bytes), opened + reading);
            }
        }
    }
}
