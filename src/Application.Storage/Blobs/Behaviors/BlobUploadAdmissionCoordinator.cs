// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Threading.RateLimiting;

/// <summary>
/// Coordinates bounded upload admission across client instances in one process.
/// </summary>
/// <example>
/// <code>
/// var snapshots = coordinator.GetSnapshots();
/// </code>
/// </example>
public interface IBlobUploadAdmissionCoordinator
{
    /// <summary>
    /// Initializes or validates the admission state for one named store.
    /// </summary>
    /// <param name="storeName">The named blob store.</param>
    /// <param name="options">The validated admission options.</param>
    /// <example>
    /// <code>
    /// coordinator.ConfigureStore("reports", options);
    /// </code>
    /// </example>
    void ConfigureStore(
        string storeName,
        UploadConcurrencyBlobStoreClientBehaviorOptions options);

    /// <summary>
    /// Acquires one upload permit for a named store.
    /// </summary>
    /// <param name="storeName">The named blob store.</param>
    /// <param name="options">The validated admission options.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>An acquired or rejected admission lease.</returns>
    /// <example>
    /// <code>
    /// await using var lease = await coordinator.AcquireAsync("reports", options, cancellationToken);
    /// </code>
    /// </example>
    ValueTask<BlobUploadAdmissionLease> AcquireAsync(
        string storeName,
        UploadConcurrencyBlobStoreClientBehaviorOptions options,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets immutable admission state snapshots for diagnostics.
    /// </summary>
    /// <returns>The current per-store snapshots.</returns>
    /// <example>
    /// <code>
    /// var active = coordinator.GetSnapshots().Sum(item => item.ActiveUploads);
    /// </code>
    /// </example>
    IReadOnlyCollection<BlobUploadAdmissionSnapshot> GetSnapshots();
}

/// <summary>
/// Implements shared bounded FIFO upload admission per normalized store name.
/// </summary>
/// <example>
/// <code>
/// using var coordinator = new BlobUploadAdmissionCoordinator();
/// </code>
/// </example>
public sealed class BlobUploadAdmissionCoordinator : IBlobUploadAdmissionCoordinator, IDisposable
{
    private readonly ConcurrentDictionary<string, StoreState> stores =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource shutdown = new();
    private readonly TimeProvider timeProvider;
    private readonly UpDownCounter<long> activeUploads;
    private readonly UpDownCounter<long> queuedUploads;
    private int disposed;

    /// <summary>
    /// Initializes a new upload-admission coordinator.
    /// </summary>
    /// <param name="timeProvider">The optional clock used for queue timeouts.</param>
    /// <param name="meterFactory">The optional meter factory used for state metrics.</param>
    /// <example>
    /// <code>
    /// using var coordinator = new BlobUploadAdmissionCoordinator(TimeProvider.System);
    /// </code>
    /// </example>
    public BlobUploadAdmissionCoordinator(
        TimeProvider timeProvider = null,
        IMeterFactory meterFactory = null)
    {
        this.timeProvider = timeProvider ?? TimeProvider.System;
        var meter = meterFactory?.Create(Metrics.MeterName);
        this.activeUploads = meter?.CreateUpDownCounter<long>("blobstorage_uploads_active");
        this.queuedUploads = meter?.CreateUpDownCounter<long>("blobstorage_uploads_queued");
    }

    /// <inheritdoc />
    public void ConfigureStore(
        string storeName,
        UploadConcurrencyBlobStoreClientBehaviorOptions options)
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref this.disposed) != 0,
            this);
        ArgumentNullException.ThrowIfNull(options);
        var validation = options.Validate();
        if (validation.IsFailure)
        {
            throw new InvalidOperationException(
                validation.Errors.FirstOrDefault()?.Message ??
                "Blob upload concurrency options are invalid.");
        }

        var normalizedStoreName = NormalizeStoreName(storeName);
        var state = this.stores.GetOrAdd(
            normalizedStoreName,
            name => new StoreState(
                name,
                options,
                this.activeUploads,
                this.queuedUploads));
        state.EnsureCompatible(options);
    }

    /// <inheritdoc />
    public async ValueTask<BlobUploadAdmissionLease> AcquireAsync(
        string storeName,
        UploadConcurrencyBlobStoreClientBehaviorOptions options,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref this.disposed) != 0,
            this);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedStoreName = NormalizeStoreName(storeName);
        this.ConfigureStore(normalizedStoreName, options);
        var state = this.stores[normalizedStoreName];

        var immediateLease = state.Limiter.AttemptAcquire(1);
        if (immediateLease.IsAcquired)
        {
            state.IncrementActive();
            return BlobUploadAdmissionLease.Acquired(
                immediateLease,
                state.DecrementActive,
                TimeSpan.Zero);
        }

        immediateLease.Dispose();

        using var timeout = new CancellationTokenSource(options.QueueWaitTimeout, this.timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token,
            this.shutdown.Token);

        var started = this.timeProvider.GetTimestamp();
        var acquisition = state.Limiter.AcquireAsync(1, linked.Token);
        var countedAsQueued = !acquisition.IsCompleted;
        if (countedAsQueued)
        {
            state.IncrementQueued();
        }

        try
        {
            var lease = await acquisition.ConfigureAwait(false);
            var waitDuration = this.timeProvider.GetElapsedTime(started);
            if (!lease.IsAcquired)
            {
                lease.Dispose();
                return BlobUploadAdmissionLease.Rejected(
                    new BlobStoreUploadOverloadedError(
                        normalizedStoreName,
                        options.MaxConcurrentUploads,
                        options.MaxQueuedUploads),
                    waitDuration);
            }

            state.IncrementActive();
            return BlobUploadAdmissionLease.Acquired(
                lease,
                state.DecrementActive,
                waitDuration);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            return BlobUploadAdmissionLease.Rejected(
                new BlobStoreUploadAdmissionTimeoutError(
                    normalizedStoreName,
                    options.QueueWaitTimeout),
                this.timeProvider.GetElapsedTime(started));
        }
        catch (OperationCanceledException) when (this.shutdown.IsCancellationRequested)
        {
            throw new ObjectDisposedException(nameof(BlobUploadAdmissionCoordinator));
        }
        finally
        {
            if (countedAsQueued)
            {
                state.DecrementQueued();
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<BlobUploadAdmissionSnapshot> GetSnapshots() =>
        this.stores.Values
            .Select(state => state.CreateSnapshot())
            .OrderBy(snapshot => snapshot.StoreName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) != 0)
        {
            return;
        }

        this.shutdown.Cancel();
        foreach (var state in this.stores.Values)
        {
            state.Dispose();
        }

        this.shutdown.Dispose();
    }

    private static string NormalizeStoreName(string storeName)
    {
        if (string.IsNullOrWhiteSpace(storeName))
        {
            throw new ArgumentException(
                "Blob store name must not be null or whitespace.",
                nameof(storeName));
        }

        return storeName.Trim().ToLowerInvariant();
    }

    private sealed class StoreState : IDisposable
    {
        private readonly UpDownCounter<long> activeUploads;
        private readonly UpDownCounter<long> queuedUploads;
        private int active;
        private int queued;

        public StoreState(
            string storeName,
            UploadConcurrencyBlobStoreClientBehaviorOptions options,
            UpDownCounter<long> activeUploads,
            UpDownCounter<long> queuedUploads)
        {
            this.StoreName = storeName;
            this.MaxConcurrentUploads = options.MaxConcurrentUploads;
            this.MaxQueuedUploads = options.MaxQueuedUploads;
            this.QueueWaitTimeout = options.QueueWaitTimeout;
            this.activeUploads = activeUploads;
            this.queuedUploads = queuedUploads;
            this.Limiter = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
            {
                PermitLimit = options.MaxConcurrentUploads,
                QueueLimit = options.MaxQueuedUploads,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
        }

        public string StoreName { get; }

        public int MaxConcurrentUploads { get; }

        public int MaxQueuedUploads { get; }

        public TimeSpan QueueWaitTimeout { get; }

        public ConcurrencyLimiter Limiter { get; }

        public void EnsureCompatible(UploadConcurrencyBlobStoreClientBehaviorOptions options)
        {
            if (this.MaxConcurrentUploads != options.MaxConcurrentUploads ||
                this.MaxQueuedUploads != options.MaxQueuedUploads ||
                this.QueueWaitTimeout != options.QueueWaitTimeout)
            {
                throw new InvalidOperationException(
                    $"Blob store '{this.StoreName}' was configured with inconsistent upload-admission limits.");
            }
        }

        public void IncrementActive()
        {
            Interlocked.Increment(ref this.active);
            RecordMetric(this.activeUploads, 1, this.StoreName);
        }

        public void DecrementActive()
        {
            Interlocked.Decrement(ref this.active);
            RecordMetric(this.activeUploads, -1, this.StoreName);
        }

        public void IncrementQueued()
        {
            Interlocked.Increment(ref this.queued);
            RecordMetric(this.queuedUploads, 1, this.StoreName);
        }

        public void DecrementQueued()
        {
            Interlocked.Decrement(ref this.queued);
            RecordMetric(this.queuedUploads, -1, this.StoreName);
        }

        public BlobUploadAdmissionSnapshot CreateSnapshot() =>
            new(
                this.StoreName,
                this.MaxConcurrentUploads,
                this.MaxQueuedUploads,
                Volatile.Read(ref this.active),
                Volatile.Read(ref this.queued));

        public void Dispose() => this.Limiter.Dispose();

        private static void RecordMetric(
            UpDownCounter<long> counter,
            long value,
            string storeName)
        {
            try
            {
                counter?.Add(value, new KeyValuePair<string, object>("store", storeName));
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and
                not StackOverflowException and
                not AccessViolationException)
            {
                // Admission state and permit ownership must not depend on metric publication.
            }
        }
    }
}

/// <summary>
/// Represents the outcome and lifetime of one upload-admission request.
/// </summary>
/// <example>
/// <code>
/// await using var lease = await coordinator.AcquireAsync("reports", options, cancellationToken);
/// if (!lease.IsAcquired)
/// {
///     return Result.Failure(lease.Error);
/// }
/// </code>
/// </example>
public sealed class BlobUploadAdmissionLease : IAsyncDisposable
{
    private readonly Action release;
    private RateLimitLease lease;

    private BlobUploadAdmissionLease(
        RateLimitLease lease,
        Action release,
        IResultError error,
        TimeSpan waitDuration)
    {
        this.lease = lease;
        this.release = release;
        this.Error = error;
        this.WaitDuration = waitDuration;
    }

    /// <summary>
    /// Gets a value indicating whether the upload was admitted.
    /// </summary>
    /// <example>
    /// <code>
    /// if (lease.IsAcquired) { /* upload */ }
    /// </code>
    /// </example>
    public bool IsAcquired => this.lease is not null;

    /// <summary>
    /// Gets the typed rejection error when admission failed.
    /// </summary>
    /// <example>
    /// <code>
    /// var error = lease.Error;
    /// </code>
    /// </example>
    public IResultError Error { get; }

    /// <summary>
    /// Gets the time spent waiting for admission.
    /// </summary>
    /// <example>
    /// <code>
    /// var wait = lease.WaitDuration;
    /// </code>
    /// </example>
    public TimeSpan WaitDuration { get; }

    /// <summary>
    /// Creates an acquired lease for coordinator implementations.
    /// </summary>
    /// <param name="lease">The underlying rate-limit lease.</param>
    /// <param name="release">The callback that updates coordinator state after release.</param>
    /// <param name="waitDuration">The admission wait duration.</param>
    /// <returns>An acquired upload-admission lease.</returns>
    /// <example>
    /// <code>
    /// var admission = BlobUploadAdmissionLease.Acquired(rateLimitLease, OnReleased, wait);
    /// </code>
    /// </example>
    public static BlobUploadAdmissionLease Acquired(
        RateLimitLease lease,
        Action release,
        TimeSpan waitDuration) =>
        new(lease, release, null, waitDuration);

    /// <summary>
    /// Creates a rejected admission lease.
    /// </summary>
    /// <param name="error">The typed rejection error.</param>
    /// <param name="waitDuration">The admission wait duration.</param>
    /// <returns>A rejected upload-admission lease.</returns>
    /// <example>
    /// <code>
    /// var admission = BlobUploadAdmissionLease.Rejected(error, wait);
    /// </code>
    /// </example>
    public static BlobUploadAdmissionLease Rejected(
        IResultError error,
        TimeSpan waitDuration) =>
        new(null, null, error, waitDuration);

    /// <summary>
    /// Releases an acquired permit. Repeated calls are safe.
    /// </summary>
    /// <returns>A completed value task.</returns>
    /// <example>
    /// <code>
    /// await lease.DisposeAsync();
    /// </code>
    /// </example>
    public ValueTask DisposeAsync()
    {
        var lease = Interlocked.Exchange(ref this.lease, null);
        if (lease is null)
        {
            return ValueTask.CompletedTask;
        }

        lease.Dispose();
        this.release();

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Describes current upload-admission limits and counts for one named store.
/// </summary>
/// <param name="StoreName">The normalized store name.</param>
/// <param name="MaxConcurrentUploads">The configured active upload limit.</param>
/// <param name="MaxQueuedUploads">The configured waiting upload limit.</param>
/// <param name="ActiveUploads">The current active upload count.</param>
/// <param name="QueuedUploads">The current queued upload count.</param>
/// <example>
/// <code>
/// var snapshot = new BlobUploadAdmissionSnapshot("reports", 4, 16, 2, 3);
/// </code>
/// </example>
public sealed record BlobUploadAdmissionSnapshot(
    string StoreName,
    int MaxConcurrentUploads,
    int MaxQueuedUploads,
    int ActiveUploads,
    int QueuedUploads);
