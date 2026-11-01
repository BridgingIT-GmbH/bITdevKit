// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Buffers;
using Microsoft.Extensions.Logging;

internal sealed class NdjsonOutputSession(Stream destination, NdjsonWriterOptions options, TimeProvider timeProvider, CancellationToken cancellationToken, TimeSpan? writeTimeout = null)
{
    private static readonly Task Never = Task.Delay(Timeout.InfiniteTimeSpan);
    private byte[] batch;
    private int batchLength;
    private long batchRecords;
    private CancellationTokenSource batchTimerCancellation;
    private Task batchDeadline = Never;
    private Task pendingRelease = Task.CompletedTask;
    private bool failed;
    private NdjsonStreamFailureStage? failureStage;
    private string reasonCode;
    private bool canceled;
    private bool partialWritePossible;
    private long writtenRecords;
    private long writtenBytes;
    private long flushedRecords;
    private long flushedBytes;

    internal CancellationToken OutputCancellation { get; set; } = cancellationToken;
    internal bool PartialWritePossible => this.partialWritePossible;
    internal long LastFlushTimestamp { get; private set; } = timeProvider.GetTimestamp();
    internal bool SourceCompleted { get; set; }

    internal bool CleanupCompleted { get; set; }

    internal Task BatchDeadline => this.batchDeadline;
    internal bool HasBatch => this.batchLength > 0;
    internal bool HasFailure => this.failed;
    internal string ReasonCode => this.reasonCode;

    internal void Fail(NdjsonStreamFailureStage stage, string code)
    {
        this.failed = true;
        if (this.failureStage is null)
        {
            this.failureStage = stage;
            this.reasonCode = code;
        }
    }

    internal void Cancel() => this.canceled = true;

    internal NdjsonStreamOutcome Outcome() => new(
        this.failed ? NdjsonStreamStatus.Failed : this.canceled ? NdjsonStreamStatus.Canceled : NdjsonStreamStatus.Succeeded,
        this.SourceCompleted, this.CleanupCompleted, this.writtenRecords, this.writtenBytes,
        this.flushedRecords, this.flushedBytes, this.partialWritePossible, this.failureStage,
        this.reasonCode ?? (this.canceled ? "stream_canceled" : null));

    internal async Task EmitOwnedAsync(NdjsonEncodedRecord record, bool immediate = false)
    {
        var transferred = false;
        try
        {
            var bytes = record.FramedBytes;
            if (options.BatchByteLimit is not { } capacity || bytes.Length > capacity)
            {
                await this.FlushBatchAsync().ConfigureAwait(false);
                transferred = true;
                await this.WriteAsync(bytes, 1, record.Dispose).ConfigureAwait(false);
                await this.FlushAsync().ConfigureAwait(false);
                return;
            }

            if (bytes.Length > capacity - this.batchLength)
            {
                await this.FlushBatchAsync().ConfigureAwait(false);
            }

            this.batch ??= ArrayPool<byte>.Shared.Rent(capacity);
            bytes.Span.CopyTo(this.batch.AsSpan(this.batchLength));
            this.batchLength += bytes.Length;
            this.batchRecords++;
            if (this.batchTimerCancellation is null)
            {
                this.batchTimerCancellation = new CancellationTokenSource();
                this.batchDeadline = Task.Delay(options.BatchMaxDelay.Value, timeProvider, this.batchTimerCancellation.Token);
            }

            if (immediate || this.batchLength == capacity)
            {
                await this.FlushBatchAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            if (!transferred)
            {
                record.Dispose();
            }
        }
    }

    internal async Task FlushBatchAsync()
    {
        if (this.batchLength == 0)
        {
            return;
        }

        var length = this.batchLength;
        var records = this.batchRecords;
        this.batchLength = 0;
        this.batchRecords = 0;
        this.StopBatchTimer();
        await this.WriteAsync(this.batch.AsMemory(0, length), records).ConfigureAwait(false);
        await this.FlushAsync().ConfigureAwait(false);
    }

    internal async Task FlushAsync()
    {
        await this.OperationAsync(token => destination.FlushAsync(token), NdjsonStreamFailureStage.Flush).ConfigureAwait(false);
        this.flushedRecords = this.writtenRecords;
        this.flushedBytes = this.writtenBytes;
        this.LastFlushTimestamp = timeProvider.GetTimestamp();
    }

    internal async Task FinishOutputAsync()
    {
        await this.FlushBatchAsync().ConfigureAwait(false);
        await this.FlushAsync().ConfigureAwait(false);
    }

    internal Task StartResponseAsync(Func<CancellationToken, Task> start) => this.OperationAsync(start, NdjsonStreamFailureStage.Write);

    private async Task WriteAsync(ReadOnlyMemory<byte> bytes, long records, Action release = null)
    {
        await this.OperationAsync(token => destination.WriteAsync(bytes, token).AsTask(), NdjsonStreamFailureStage.Write, release).ConfigureAwait(false);
        this.writtenRecords = checked(this.writtenRecords + records);
        this.writtenBytes = checked(this.writtenBytes + bytes.Length);
    }

    private async Task OperationAsync(Func<CancellationToken, Task> operation, NdjsonStreamFailureStage stage, Action release = null)
    {
        CancellationTokenSource deadline = null;
        CancellationTokenSource linked = null;
        Task pending = null;
        var deferredRelease = false;
        try
        {
            if (writeTimeout is { } timeout)
            {
                deadline = new CancellationTokenSource(timeout, timeProvider);
                linked = CancellationTokenSource.CreateLinkedTokenSource(this.OutputCancellation, deadline.Token);
            }

            var token = linked?.Token ?? this.OutputCancellation;
            pending = operation(token);
            if (writeTimeout is { } limit)
            {
                await pending.WaitAsync(limit, timeProvider, this.OutputCancellation).ConfigureAwait(false);
            }
            else
            {
                await pending.WaitAsync(this.OutputCancellation).ConfigureAwait(false);
            }
        }
        catch (Exception error)
        {
            this.partialWritePossible = true;
            if (pending is { IsCompleted: false })
            {
                deferredRelease = true;
                this.pendingRelease = ReleaseLaterAsync(pending, release, linked, deadline);
            }

            if (error is OperationCanceledException && this.OutputCancellation.IsCancellationRequested)
            {
                this.Cancel();
                throw;
            }

            var timeout = error is TimeoutException || deadline?.IsCancellationRequested == true;
            var code = timeout ? "stream_write_timeout" : stage == NdjsonStreamFailureStage.Flush ? "stream_flush_failed" : "stream_write_failed";
            this.Fail(stage, code);
            throw new NdjsonStreamException(code);
        }
        finally
        {
            if (!deferredRelease)
            {
                release?.Invoke();
                linked?.Dispose();
                deadline?.Dispose();
            }
        }

        static async Task ReleaseLaterAsync(Task operation, Action release, CancellationTokenSource linked, CancellationTokenSource deadline)
        {
            try
            {
                await NdjsonCleanupCoordinator.ObserveAsync(operation).ConfigureAwait(false);
            }
            finally
            {
                release?.Invoke();
                linked?.Dispose();
                deadline?.Dispose();
            }
        }
    }

    internal void StopBatchTimer()
    {
        this.batchTimerCancellation?.Cancel();
        this.batchTimerCancellation?.Dispose();
        this.batchTimerCancellation = null;
        this.batchDeadline = Never;
    }

    internal async Task ReleaseAsync()
    {
        this.StopBatchTimer();
        await this.pendingRelease.ConfigureAwait(false);
        if (this.batch is not null)
        {
            ArrayPool<byte>.Shared.Return(this.batch, clearArray: true);
            this.batch = null;
        }
    }

    internal static async Task NotifyAsync(INdjsonStreamObserver observer, NdjsonStreamOutcome outcome, TimeSpan timeout, TimeProvider timeProvider, CancellationToken hostShutdown = default, ILogger logger = null)
    {
        if (observer is null)
        {
            return;
        }

        using var budget = new CancellationTokenSource(timeout, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(budget.Token, hostShutdown);
        Task notification = null;
        try
        {
            notification = observer.OnFinishedAsync(outcome, linked.Token);
            await notification.WaitAsync(timeout, timeProvider, hostShutdown).ConfigureAwait(false);
        }
        catch (Exception)
        {
            NdjsonDiagnostics.Report(logger, budget.IsCancellationRequested ? "stream_observer_timeout" : "stream_observer_failed");
            if (notification is not null)
            {
                _ = NdjsonCleanupCoordinator.ObserveAsync(notification, logger, "stream_observer_failed");
            }
        }
    }
}
