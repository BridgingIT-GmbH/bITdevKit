// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Auditing;

using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing;
using Microsoft.Extensions.Time.Testing;
using System.Threading.Channels;

/// <summary>Checks bounded admission and confirmed delivery across writer boundaries.</summary>
/// <example><code>await queue.FlushAsync();</code></example>
public class AuditWriteQueueTests
{
    /// <summary>Retains both record and byte capacity while an append is in flight.</summary>
    /// <example><code>queue.PendingBytes.ShouldBe(encodedLength);</code></example>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TryAppend_InFlightRecord_RetainsCountAndByteCapacity(bool countLimit)
    {
        var record = Record();
        var encodedLength = AuditRecordCodec.Encode(record).Length;
        var entered = Barrier();
        var release = Barrier();
        var options = Options() with { QueueRecordLimit = countLimit ? 1 : 2, QueueByteLimit = countLimit ? 1_000_000 : encodedLength };
        var sut = new AuditWriteQueue(async (value, token) =>
        {
            entered.SetResult();
            await release.Task.WaitAsync(token);
            return Confirmed(value);
        }, options, TimeProvider.System);
        sut.TryAppend(record).IsSuccess.ShouldBeTrue();
        var worker = sut.RunAsync(default);

        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            sut.PendingBytes.ShouldBe(encodedLength);
            sut.Snapshot().Pending.ShouldBe(1);
            var rejected = sut.TryAppend(Record());
            rejected.IsFailure.ShouldBeTrue();
            rejected.Errors.OfType<AuditError>().Single().Code.ShouldBe("AuditQueueFull");
        }
        finally
        {
            release.TrySetResult();
            sut.CloseAdmissions();
            await worker.WaitAsync(TimeSpan.FromSeconds(10));
        }

        sut.PendingBytes.ShouldBe(0);
        sut.Snapshot().Persisted.ShouldBe(1);
    }

    /// <summary>Reuses an in-flight receipt only when the immutable content matches.</summary>
    /// <example><code>duplicate.Value.ShouldBeSameAs(original.Value);</code></example>
    [Fact]
    public async Task TryAppend_InFlightDuplicate_ReusesReceiptAndRejectsConflictingContent()
    {
        var record = Record();
        var entered = Barrier();
        var release = Barrier();
        var sut = new AuditWriteQueue(async (value, token) =>
        {
            entered.SetResult();
            await release.Task.WaitAsync(token);
            return Confirmed(value);
        }, Options() with { QueueRecordLimit = 1 }, TimeProvider.System);
        var original = sut.TryAppend(record);
        var worker = sut.RunAsync(default);

        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var duplicate = sut.TryAppend(record);
            duplicate.IsSuccess.ShouldBeTrue();
            duplicate.Value.ShouldBeSameAs(original.Value);
            var conflict = sut.TryAppend(record with { Key = "orders.reject", ContentHash = null });
            conflict.IsFailure.ShouldBeTrue();
            conflict.Errors.OfType<AuditError>().Single().Code.ShouldBe("AuditIdentityConflict");
            sut.Snapshot().ThroughSequence.ShouldBe(1);
        }
        finally
        {
            release.TrySetResult();
            sut.CloseAdmissions();
            await worker.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    /// <summary>Retries the exact frozen envelope before advancing to the next admission.</summary>
    /// <example><code>attempts[0].ShouldBeSameAs(attempts[1]);</code></example>
    [Fact]
    public async Task RunAsync_TransientFailure_RetriesSameFrozenEnvelopeInFifoOrder()
    {
        var first = Record() with { IntegrityProtection = new() { KeyId = "test-key", Tag = Convert.ToBase64String(new byte[32]) } };
        var second = Record();
        var attempts = new List<AuditRecord>();
        var sut = new AuditWriteQueue((record, _) =>
        {
            attempts.Add(record);
            return Task.FromResult(attempts.Count == 1
                ? Result<AuditAppendReceipt>.Failure(new AuditError("AuditStoreUnavailable"))
                : Confirmed(record));
        }, Options() with { RetryCount = 1, RetryDelay = TimeSpan.Zero }, TimeProvider.System);
        sut.TryAppend(first).IsSuccess.ShouldBeTrue();
        sut.TryAppend(second).IsSuccess.ShouldBeTrue();
        sut.CloseAdmissions();

        await sut.RunAsync(default).WaitAsync(TimeSpan.FromSeconds(10));

        attempts.Select(record => record.AuditId).ShouldBe([first.AuditId, first.AuditId, second.AuditId]);
        attempts[0].ShouldNotBeSameAs(first);
        attempts[1].ShouldBeSameAs(attempts[0]);
        AuditRecordCodec.Encode(attempts[0]).ShouldBe(AuditRecordCodec.Encode(first));
        attempts[0].IntegrityProtection.ShouldBe(first.IntegrityProtection);
        var flush = await sut.FlushAsync();
        flush.IsSuccess.ShouldBeTrue();
        flush.Value.Persisted.ShouldBe(2);
    }

    /// <summary>Counts only durable-presence receipts as successful delivery.</summary>
    /// <example><code>flush.Value.Persisted.ShouldBe(1);</code></example>
    [Theory]
    [InlineData("Persisted", true)]
    [InlineData("AlreadyPresent", true)]
    [InlineData("Queued", false)]
    [InlineData("Unknown", false)]
    public async Task RunAsync_ReceiptState_RequiresConfirmedPersistence(string state, bool confirmed)
    {
        var sut = new AuditWriteQueue((record, _) => Task.FromResult(Confirmed(record, state)), Options(), TimeProvider.System);
        sut.TryAppend(Record()).IsSuccess.ShouldBeTrue();
        sut.CloseAdmissions();

        await sut.RunAsync(default).WaitAsync(TimeSpan.FromSeconds(10));
        var flush = await sut.FlushAsync();

        flush.IsSuccess.ShouldBe(confirmed);
        var counts = confirmed ? flush.Value : Incomplete(flush);
        counts.Persisted.ShouldBe(confirmed ? 1 : 0);
        counts.Failed.ShouldBe(confirmed ? 0 : 1);
        counts.Pending.ShouldBe(0);
    }

    /// <summary>Captures a flush boundary without waiting for subsequently admitted work.</summary>
    /// <example><code>flush.Value.ThroughSequence.ShouldBe(firstReceipt.AcceptanceSequence);</code></example>
    [Fact]
    public async Task FlushAsync_LaterAdmission_ExcludesLaterInFlightRecord()
    {
        var first = Record();
        var firstEntered = Barrier();
        var firstRelease = Barrier();
        var secondEntered = Barrier();
        var secondRelease = Barrier();
        var sut = new AuditWriteQueue(async (record, token) =>
        {
            if (record.AuditId == first.AuditId)
            {
                firstEntered.SetResult();
                await firstRelease.Task.WaitAsync(token);
            }
            else
            {
                secondEntered.SetResult();
                await secondRelease.Task.WaitAsync(token);
            }

            return Confirmed(record);
        }, Options(), TimeProvider.System);
        var receipt = sut.TryAppend(first).Value;
        var worker = sut.RunAsync(default);

        try
        {
            await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var flush = sut.FlushAsync();
            sut.TryAppend(Record()).IsSuccess.ShouldBeTrue();
            firstRelease.SetResult();
            await secondEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var result = await flush.WaitAsync(TimeSpan.FromSeconds(10));
            result.IsSuccess.ShouldBeTrue();
            result.Value.ReceiptEpoch.ShouldBe(receipt.ReceiptEpoch);
            result.Value.ThroughSequence.ShouldBe(receipt.AcceptanceSequence);
            result.Value.Persisted.ShouldBe(1);
            result.Value.Pending.ShouldBe(0);
            sut.Snapshot().Pending.ShouldBe(1);
        }
        finally
        {
            firstRelease.TrySetResult();
            secondRelease.TrySetResult();
            sut.CloseAdmissions();
            await worker.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    /// <summary>Canceling one flush leaves its accepted append running to confirmation.</summary>
    /// <example><code>writerToken.IsCancellationRequested.ShouldBeFalse();</code></example>
    [Fact]
    public async Task FlushAsync_CanceledWait_DoesNotCancelWriter()
    {
        var entered = Barrier();
        var release = Barrier();
        var writerToken = default(CancellationToken);
        var sut = new AuditWriteQueue(async (record, token) =>
        {
            writerToken = token;
            entered.SetResult();
            await release.Task.WaitAsync(token);
            return Confirmed(record);
        }, Options(), TimeProvider.System);
        sut.TryAppend(Record()).IsSuccess.ShouldBeTrue();
        var worker = sut.RunAsync(default);

        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            using var cancellation = new CancellationTokenSource();
            var flush = sut.FlushAsync(cancellation.Token);
            cancellation.Cancel();
            var counts = Incomplete(await flush.WaitAsync(TimeSpan.FromSeconds(10)));
            counts.Pending.ShouldBe(1);
            counts.Failed.ShouldBe(0);
            writerToken.IsCancellationRequested.ShouldBeFalse();
            release.SetResult();
            (await sut.FlushAsync()).IsSuccess.ShouldBeTrue();
        }
        finally
        {
            release.TrySetResult();
            sut.CloseAdmissions();
            await worker.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    /// <summary>A timed-out flush preserves pending admissions for the owned writer.</summary>
    /// <example><code>Incomplete(result).Pending.ShouldBe(1);</code></example>
    [Fact]
    public async Task FlushAsync_TimeoutBeforeWriterStarts_PreservesAcceptedWork()
    {
        var calls = 0;
        var sut = new AuditWriteQueue((record, _) =>
        {
            calls++;
            return Task.FromResult(Confirmed(record));
        }, Options() with { OperationTimeout = TimeSpan.FromMilliseconds(10) }, TimeProvider.System);
        sut.TryAppend(Record()).IsSuccess.ShouldBeTrue();

        var timedOut = await sut.FlushAsync().WaitAsync(TimeSpan.FromSeconds(10));

        var counts = Incomplete(timedOut);
        counts.Pending.ShouldBe(1);
        counts.Persisted.ShouldBe(0);
        calls.ShouldBe(0);
        sut.CloseAdmissions();
        await sut.RunAsync(default).WaitAsync(TimeSpan.FromSeconds(10));
        (await sut.FlushAsync()).IsSuccess.ShouldBeTrue();
        calls.ShouldBe(1);
    }

    /// <summary>Retains terminal failures across an empty queue and later successful admissions.</summary>
    /// <example><code>Incomplete(await queue.FlushAsync()).Failed.ShouldBe(1);</code></example>
    [Fact]
    public async Task FlushAsync_EarlierTerminalFailure_RetainsCumulativeFailureAfterLaterSuccess()
    {
        var failed = Record();
        var reports = new List<(Guid Id, string Code)>();
        var sut = new AuditWriteQueue((record, _) => Task.FromResult(record.AuditId == failed.AuditId
            ? Result<AuditAppendReceipt>.Failure(new AuditError("AuditIdentityConflict"))
            : Confirmed(record)), Options() with { RetryCount = 1 }, TimeProvider.System, (id, code) => reports.Add((id, code)));
        sut.TryAppend(failed).IsSuccess.ShouldBeTrue();
        var worker = sut.RunAsync(default);

        try
        {
            var first = Incomplete(await sut.FlushAsync());
            first.Failed.ShouldBe(1);
            first.Pending.ShouldBe(0);
            sut.PendingBytes.ShouldBe(0);
            Incomplete(await sut.FlushAsync()).Failed.ShouldBe(1);
            sut.TryAppend(Record()).IsSuccess.ShouldBeTrue();
            var later = Incomplete(await sut.FlushAsync());
            later.ThroughSequence.ShouldBe(2);
            later.Persisted.ShouldBe(1);
            later.Failed.ShouldBe(1);
            later.Pending.ShouldBe(0);
        }
        finally
        {
            sut.CloseAdmissions();
            await worker.WaitAsync(TimeSpan.FromSeconds(10));
        }

        reports.ShouldBe([(failed.AuditId, "AuditDeliveryFailed")]);
    }

    /// <summary>Uses the configured clock to release a partial batch without a flush.</summary>
    /// <example><code>time.Advance(options.BatchInterval);</code></example>
    [Fact]
    public async Task RunAsync_PartialBatch_WaitsForConfiguredClockInterval()
    {
        var time = new FakeTimeProvider();
        var entered = Barrier();
        var options = Options() with { BatchSize = 2, BatchInterval = TimeSpan.FromSeconds(5) };
        var sut = new AuditWriteQueue((record, _) =>
        {
            entered.SetResult();
            return Task.FromResult(Confirmed(record));
        }, options, time);
        var worker = sut.RunAsync(default);

        try
        {
            sut.TryAppend(Record()).IsSuccess.ShouldBeTrue();
            time.Advance(TimeSpan.FromSeconds(4));
            entered.Task.IsCompleted.ShouldBeFalse();
            time.Advance(TimeSpan.FromSeconds(1));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            (await sut.FlushAsync()).IsSuccess.ShouldBeTrue();
        }
        finally
        {
            sut.CloseAdmissions();
            await worker.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    /// <summary>Uses the shared resilience clock for finite exponential retries capped at two seconds.</summary>
    /// <example><code>options.RetryDelay.ShouldBe(TimeSpan.FromMilliseconds(250));</code></example>
    [Theory]
    [InlineData(3)]
    [InlineData(6)]
    public async Task RunAsync_ExponentialRetries_AreFiniteAndCapped(int retries)
    {
        var defaults = new AuditEntityFrameworkOptions();
        defaults.BatchSize.ShouldBe(100);
        defaults.BatchInterval.ShouldBe(TimeSpan.FromSeconds(1));
        defaults.RetryCount.ShouldBe(3);
        defaults.RetryDelay.ShouldBe(TimeSpan.FromMilliseconds(250));
        var time = new RetryClock();
        var calls = 0;
        var sut = new AuditWriteQueue((_, _) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(Result<AuditAppendReceipt>.Failure(new AuditError("AuditStoreUnavailable")));
        }, Options() with { RetryCount = retries }, time);
        sut.TryAppend(Record()).IsSuccess.ShouldBeTrue();
        sut.CloseAdmissions();
        var worker = sut.RunAsync(default);
        for (var attempt = 0; attempt < retries; attempt++)
        {
            var delay = await time.Delays.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
            delay.ShouldBe(TimeSpan.FromMilliseconds(Math.Min(250 * (1 << attempt), 2_000)));
            time.Clock.Advance(delay);
        }

        await worker.WaitAsync(TimeSpan.FromSeconds(10));
        calls.ShouldBe(retries + 1);
        Incomplete(await sut.FlushAsync()).Failed.ShouldBe(1);
    }

    private sealed class RetryClock : TimeProvider
    {
        public FakeTimeProvider Clock { get; } = new();
        public Channel<TimeSpan> Delays { get; } = Channel.CreateUnbounded<TimeSpan>();
        public override DateTimeOffset GetUtcNow() => this.Clock.GetUtcNow();
        public override long GetTimestamp() => this.Clock.GetTimestamp();
        public override long TimestampFrequency => this.Clock.TimestampFrequency;
        public override ITimer CreateTimer(TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = this.Clock.CreateTimer(callback, state, dueTime, period);
            if (period == Timeout.InfiniteTimeSpan && dueTime > TimeSpan.Zero && dueTime <= TimeSpan.FromSeconds(2)) this.Delays.Writer.TryWrite(dueTime);
            return timer;
        }
    }

    private static AuditEntityFrameworkOptions Options() => new() { BatchSize = 1, RetryCount = 0, OperationTimeout = TimeSpan.FromMinutes(1) };

    private static TaskCompletionSource Barrier() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static Result<AuditAppendReceipt> Confirmed(AuditRecord record, string state = "Persisted") => Result<AuditAppendReceipt>.Success(new() { AuditId = record.AuditId, State = state });

    private static AuditFlushResult Incomplete(Result<AuditFlushResult> result)
    {
        result.IsFailure.ShouldBeTrue();
        return result.Errors.OfType<AuditFlushIncompleteError>().Single().FlushResult;
    }

    private static AuditRecord Record()
    {
        var id = Guid.NewGuid();
        return AuditRecordCodec.Freeze(new AuditRecord
        {
            AuditId = id, OriginAuditId = id, Key = "orders.approve",
            StartedUtc = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            CompletedUtc = new DateTimeOffset(2026, 1, 2, 3, 4, 6, TimeSpan.Zero),
            Outcome = AuditOutcome.Succeeded
        }).Value;
    }
}