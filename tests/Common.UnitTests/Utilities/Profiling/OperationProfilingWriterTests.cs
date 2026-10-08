// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

using Microsoft.Extensions.Time.Testing;

/// <summary>Verifies bounded queue ownership, scheduling and uncertain write settlement.</summary>
public sealed class OperationProfilingWriterTests
{
    /// <summary>Checks that only an active original lease can publish completions.</summary>
    [Fact]
    public async Task Completion_BeforeActivation_DropsButLiveScopeCanFinishAfterActivation()
    {
        var h = Create();
        Capture(h, "unavailable");
        using var active = h.Profiler.BeginOperation("started-before-lease");
        h.Queue.WriterUnavailableDiscards.ShouldBe(1);
        h.Queue.Snapshot().Count.ShouldBe(0);
        await h.Writer.TickAsync();
        active.Complete();
        active.Dispose();
        h.Queue.Snapshot().Count.ShouldBe(1);
        await h.Writer.TickAsync();
        h.Health.Persisted.ShouldBe(1);
        h.Profiler.Current.ShouldBeNull();
    }

    /// <summary>Checks shared queue and in-flight capacity with explicit rejection.</summary>
    [Fact]
    public async Task Queue_FullOrByteBound_RejectsIncomingWithoutLosingAcceptedPayload()
    {
        var h = Create(o => { o.QueueCapacity = 2; o.BatchSize = 1; o.QueueBytes = 8192; o.BatchBytes = 4096; });
        await h.Writer.TickAsync();
        Capture(h, "one");
        Capture(h, "two");
        Capture(h, "three");
        var selected = h.Queue.Select(h.Queue.Watermark());
        selected.Count.ShouldBe(1);
        h.Queue.Snapshot().Count.ShouldBe(2);
        h.Queue.CapacityDiscards.ShouldBe(1);
        h.Queue.Snapshot().Bytes.ShouldBeGreaterThan(0);
        await h.Writer.TickAsync();
        h.Health.Persisted.ShouldBe(2);
        h.Queue.Snapshot().Bytes.ShouldBe(0);
    }

    /// <summary>Checks byte limits independently from record count.</summary>
    [Fact]
    public async Task Queue_ChargedByteLimit_RejectsRecordWithoutReusingSequence()
    {
        var h = Create(o => { o.QueueBytes = 3000; o.BatchBytes = 3000; });
        await h.Writer.TickAsync();
        Capture(h, "first");
        Capture(h, "second");
        h.Queue.Snapshot().Count.ShouldBe(1);
        h.Queue.Watermark().Sequence.ShouldBe(2);
        h.Queue.CapacityDiscards.ShouldBe(1);
        await h.Writer.TickAsync();
        h.Queue.Settlement.ShouldBe(2);
    }

    /// <summary>Checks fixed per-tick arrivals and the maximum number of append starts.</summary>
    [Fact]
    public async Task Flush_ContinuousArrivals_StopsAtOriginalWatermarkAndStartCount()
    {
        var h = Create(o => { o.BatchSize = 2; o.MaxBatchesPerFlush = 2; });
        await h.Writer.TickAsync();
        for (var i = 0; i < 7; i++) { Capture(h, "initial"); }

        var starts = 0;
        h.Store.AppendAsync(Arg.Any<IReadOnlyList<ProfilingWriteEnvelope>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            starts++;
            Capture(h, "arrived-during-flush");
            return h.Real.Operations.AppendAsync(call.Arg<IReadOnlyList<ProfilingWriteEnvelope>>(), call.Arg<CancellationToken>());
        });
        await h.Writer.TickAsync();
        starts.ShouldBe(2);
        h.Health.Persisted.ShouldBe(4);
        h.Queue.Snapshot().Count.ShouldBe(5);
        h.Queue.Select(h.Queue.Watermark()).Select(e => e.Envelope.CompletionSequence).ShouldBe([5, 6]);
    }

    /// <summary>Checks that database latency consumes the next-batch scheduling budget.</summary>
    [Fact]
    public async Task Flush_TimeBudget_DoesNotStartAnotherBatch()
    {
        var h = Create(o => o.BatchSize = 1);
        await h.Writer.TickAsync();
        Capture(h); Capture(h);
        var starts = 0;
        h.Store.AppendAsync(Arg.Any<IReadOnlyList<ProfilingWriteEnvelope>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            starts++;
            var result = h.Real.Operations.AppendAsync(call.Arg<IReadOnlyList<ProfilingWriteEnvelope>>(), call.Arg<CancellationToken>());
            h.Clock.Advance(TimeSpan.FromMilliseconds(300));
            return result;
        });
        await h.Writer.TickAsync();
        starts.ShouldBe(1);
        h.Queue.Snapshot().Count.ShouldBe(1);
    }

    /// <summary>Checks successful partial members are not retried with failed siblings.</summary>
    [Fact]
    public async Task PartialBatch_RetryableMember_RetainsOnlyUnresolvedOriginalIdentity()
    {
        var h = Create();
        await h.Writer.TickAsync();
        Capture(h, "first"); Capture(h, "second");
        var original = h.Queue.Select(h.Queue.Watermark()).ToArray();
        var attempts = 0;
        h.Store.AppendAsync(Arg.Any<IReadOnlyList<ProfilingWriteEnvelope>>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var batch = call.Arg<IReadOnlyList<ProfilingWriteEnvelope>>();
            attempts++;
            if (attempts > 1)
            {
                batch.ShouldHaveSingleItem().ShouldBeSameAs(original[1].Envelope);
                return await h.Real.Operations.AppendAsync(batch);
            }

            var stored = await h.Real.Operations.AppendAsync([batch[0]]);
            return Result<ProfilingBatchWriteResult>.Success(new() { Records = [stored.Value.Records[0], new()
            {
                OperationId = batch[1].Record.Id, CompletionSequence = batch[1].CompletionSequence, Outcome = ProfilingWriteOutcome.TransientFailure,
            }] });
        });
        await h.Writer.TickAsync();
        h.Queue.Snapshot().Count.ShouldBe(1);
        h.Health.Persisted.ShouldBe(1);
        h.Queue.Settlement.ShouldBe(1);
        h.Clock.Advance(TimeSpan.FromSeconds(1));
        await h.Writer.TickAsync();
        attempts.ShouldBe(2);
        h.Health.Persisted.ShouldBe(2);
        h.Queue.Snapshot().Bytes.ShouldBe(0);
    }

    /// <summary>Checks unknown commits resolve through idempotency without duplicate graphs.</summary>
    [Fact]
    public async Task LostAcknowledgement_AfterCommit_OriginalRetryResolvesAlreadyStored()
    {
        var h = Create();
        await h.Writer.TickAsync();
        Capture(h);
        var attempts = 0;
        h.Store.AppendAsync(Arg.Any<IReadOnlyList<ProfilingWriteEnvelope>>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var result = await h.Real.Operations.AppendAsync(call.Arg<IReadOnlyList<ProfilingWriteEnvelope>>());
            return ++attempts == 1 ? Result<ProfilingBatchWriteResult>.Failure(new ProfilingPersistenceError(true, true)) : result;
        });
        await h.Writer.TickAsync();
        h.Queue.Snapshot().Count.ShouldBe(1);
        h.Clock.Advance(TimeSpan.FromSeconds(1));
        await h.Writer.TickAsync();
        h.Health.Persisted.ShouldBe(1);
        h.Health.Unknown.ShouldBe(0);
        h.Queue.Snapshot().Count.ShouldBe(0);
    }

    /// <summary>Checks exhausted uncertainty is fenced before any delayed replay.</summary>
    [Fact]
    public async Task UnknownExhaustion_SettlesSequenceWithoutClaimingConfirmedLoss()
    {
        var h = Create();
        await h.Writer.TickAsync();
        Capture(h);
        var envelope = h.Queue.Select(h.Queue.Watermark()).Single().Envelope;
        h.Store.AppendAsync(Arg.Any<IReadOnlyList<ProfilingWriteEnvelope>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IResult<ProfilingBatchWriteResult>>(Result<ProfilingBatchWriteResult>.Failure(new ProfilingPersistenceError(true, true))));
        await h.Writer.TickAsync();
        h.Clock.Advance(TimeSpan.FromSeconds(1)); await h.Writer.TickAsync();
        h.Clock.Advance(TimeSpan.FromSeconds(2)); await h.Writer.TickAsync();
        h.Health.Unknown.ShouldBe(1);
        h.Health.Lost.ShouldBe(0);
        h.Health.Retries.ShouldBe(2);
        h.Queue.Snapshot().Count.ShouldBe(0);
        var delayed = await h.Real.Operations.AppendAsync([envelope]);
        delayed.Value.Records.Single().Outcome.ShouldBe(ProfilingWriteOutcome.AlreadySettled);
    }

    /// <summary>Checks permanent failures release capacity without retrying.</summary>
    [Fact]
    public async Task PermanentFailure_ReleasesPayloadAndAdvancesSettlement()
    {
        var h = Create();
        await h.Writer.TickAsync(); Capture(h);
        h.Store.AppendAsync(Arg.Any<IReadOnlyList<ProfilingWriteEnvelope>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IResult<ProfilingBatchWriteResult>>(Result<ProfilingBatchWriteResult>.Failure(new ProfilingPersistenceError(false, false))));
        await h.Writer.TickAsync();
        h.Health.Lost.ShouldBe(1);
        h.Health.Retries.ShouldBe(0);
        h.Queue.Snapshot().Bytes.ShouldBe(0);
        h.Queue.Settlement.ShouldBe(1);
    }

    /// <summary>Checks unreachable providers leave business capture bounded and available.</summary>
    [Fact]
    public async Task StartupOutage_ReusesOpenAttemptAndDoesNotFailCapture()
    {
        var h = Create();
        var attempts = new List<Guid>();
        h.Store.OpenWriterAsync(Arg.Any<ProfilingOpenWriterRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            attempts.Add(call.Arg<ProfilingOpenWriterRequest>().AttemptId);
            return Task.FromResult<IResult<ProfilingWriterLease>>(Result<ProfilingWriterLease>.Failure(new ProfilingPersistenceError(true, true)));
        });
        await h.Writer.TickAsync(); Capture(h); await h.Writer.TickAsync();
        attempts.Distinct().Count().ShouldBe(1);
        h.Queue.WriterUnavailableDiscards.ShouldBe(1);
        h.Profiler.ActiveCount.ShouldBe(0);
        h.Health.GetSnapshot().WriterActive.ShouldBeFalse();
    }

    /// <summary>Checks old buffers are discarded before the replacement lease activates.</summary>
    [Fact]
    public async Task ExpiredLease_DoesNotRelabelPendingEnvelopes()
    {
        var h = Create();
        await h.Writer.TickAsync(); Capture(h);
        var original = h.Queue.Watermark().Lease;
        h.Clock.Advance(TimeSpan.FromSeconds(31));
        await h.Writer.TickAsync();
        h.Queue.ExpiredLeaseDiscards.ShouldBe(1);
        h.Queue.Snapshot().Active.ShouldBeFalse();
        await h.Writer.TickAsync();
        h.Queue.Watermark().Lease.Token.ShouldNotBe(original.Token);
        h.Queue.Watermark().Sequence.ShouldBe(0);
    }

    /// <summary>Checks ignored cancellation cannot lead to overlapping append attempts.</summary>
    [Fact]
    public async Task IgnoredTimeout_DoesNotOverlapOrSettleUntilCallReturns()
    {
        var h = Create();
        await h.Writer.TickAsync(); Capture(h);
        var release = new TaskCompletionSource<IResult<ProfilingBatchWriteResult>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var starts = 0;
        IReadOnlyList<ProfilingWriteEnvelope> original = null;
        h.Store.AppendAsync(Arg.Any<IReadOnlyList<ProfilingWriteEnvelope>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            starts++; original = call.Arg<IReadOnlyList<ProfilingWriteEnvelope>>(); entered.TrySetResult(); return release.Task;
        });
        var blocked = h.Writer.TickAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        h.Clock.Advance(TimeSpan.FromSeconds(6));
        await WaitUntil(() => h.Health.Stalled);
        await h.Writer.TickAsync();
        starts.ShouldBe(1);
        h.Queue.Settlement.ShouldBe(0);
        h.Queue.Snapshot().Count.ShouldBe(1);
        release.SetResult(await h.Real.Operations.AppendAsync(original));
        await blocked.WaitAsync(TimeSpan.FromSeconds(5));
        h.Health.Stalled.ShouldBeFalse();
        h.Health.Persisted.ShouldBe(1);
    }

    /// <summary>Checks idle ticks participate in cross-writer clear fencing.</summary>
    [Fact]
    public async Task IdleTick_AcknowledgesClearAndPreservesFixedCutoffAfterLostResponse()
    {
        var h = Create();
        await h.Writer.TickAsync(); Capture(h);
        var cutoff = h.Queue.Watermark().Sequence;
        var clears = new List<long>();
        var attempt = 0;
        Task<IResult<ProfilingClearResult>> clearing = null;
        h.Store.AcknowledgeClearAsync(Arg.Any<ProfilingClearAcknowledgement>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var acknowledgement = call.Arg<ProfilingClearAcknowledgement>();
            clears.Add(acknowledgement.CompletionCutoff);
            var result = await h.Real.Operations.AcknowledgeClearAsync(acknowledgement);
            if (++attempt == 1)
            {
                Capture(h, "post-cutoff");
                return Result<ProfilingClearAcknowledgement>.Failure(new ProfilingPersistenceError(true, true));
            }

            // Make the append exercise the sealed fence, rather than racing the clear coordinator.
            await clearing;
            return result;
        });
        clearing = h.Real.ClearAsync(new() { DataSet = ProfilingDataSet.Operations });
        await h.Writer.TickAsync();
        await h.Writer.TickAsync();
        (await clearing.WaitAsync(TimeSpan.FromSeconds(5))).IsSuccess.ShouldBeTrue();
        clears.ShouldBe([cutoff, cutoff]);
        await h.Writer.TickAsync();
        h.Health.Administrative.ShouldBe(1);
        h.Health.Persisted.ShouldBe(1);
    }

    /// <summary>Checks the nominal scheduler ceiling independently of database latency.</summary>
    [Fact]
    public async Task DefaultTick_AtMostEight512RecordBatches_LeavesLaterWorkQueued()
    {
        var h = Create();
        await h.Writer.TickAsync();
        for (var i = 0; i < 5000; i++) { Capture(h); }

        await h.Writer.TickAsync();
        h.Health.Persisted.ShouldBe(4096);
        h.Queue.Snapshot().Count.ShouldBe(904);
    }

    /// <summary>Checks byte-bound batches can be smaller than their record limit.</summary>
    [Fact]
    public async Task Batch_ByteBound_StopsBeforeExceedingChargedPayload()
    {
        var h = Create(o => { o.BatchBytes = 5000; o.MaxBatchesPerFlush = 1; });
        await h.Writer.TickAsync();
        Capture(h); Capture(h); Capture(h);
        var batch = h.Queue.Select(h.Queue.Watermark());
        batch.Count.ShouldBe(2);
        batch.Sum(e => e.Envelope.Record.EstimatedPayloadBytes).ShouldBeLessThanOrEqualTo(5000);
        await h.Writer.TickAsync();
        h.Queue.Snapshot().Count.ShouldBe(1);
    }

    /// <summary>Checks shutdown closes active roots before draining the queue.</summary>
    [Fact]
    public async Task Shutdown_ClosesActiveCaptureBeforeDrain_WithoutChangingBusinessLifetime()
    {
        var h = Create();
        await h.Writer.StartAsync(default);
        await WaitUntil(() => h.Queue.Snapshot().Active);
        using var operation = h.Profiler.BeginOperation("still-running");
        var id = operation.Id;
        await h.Writer.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(5));
        var stored = await h.Real.Operations.FindAsync(id);
        stored.IsSuccess.ShouldBeTrue();
        stored.Value.Outcome.ShouldBe(OperationProfilingOutcome.Incomplete);
        stored.Value.Quality.IncompleteReason.ShouldBe(ProfilingIncompleteReason.HostStopping);
        h.Profiler.ActiveCount.ShouldBe(0);
        h.Queue.Snapshot().Bytes.ShouldBe(0);
        operation.IsRecording.ShouldBeFalse();
    }

    /// <summary>Checks shutdown deadlines remain bounded even for cancellation-ignoring stores.</summary>
    [Fact]
    public async Task Shutdown_IgnoredCall_ReportsUnknownWithoutOverlappingOrCountingConfirmedLoss()
    {
        var h = Create();
        await h.Writer.TickAsync(); Capture(h);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<IResult<ProfilingBatchWriteResult>>(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Store.AppendAsync(Arg.Any<IReadOnlyList<ProfilingWriteEnvelope>>(), Arg.Any<CancellationToken>()).Returns(_ => { entered.TrySetResult(); return release.Task; });
        var blocked = h.Writer.TickAsync();
        await entered.Task;
        h.Health.GetSnapshot().InFlightRecords.ShouldBe(1);
        var stopping = h.Writer.StopAsync(default);
        h.Clock.Advance(TimeSpan.FromSeconds(6));
        await stopping.WaitAsync(TimeSpan.FromSeconds(5));
        h.Health.Unknown.ShouldBe(1);
        h.Health.GetSnapshot().DroppedOperations.ShouldBe(0);
        h.Queue.Snapshot().Count.ShouldBe(0);
        release.SetResult(Result<ProfilingBatchWriteResult>.Failure(new ProfilingPersistenceError(true, true)));
        await blocked.WaitAsync(TimeSpan.FromSeconds(5));
        h.Health.Unknown.ShouldBe(1);
    }

    private static Harness Create(Action<OperationProfilingOptions> configure = null)
    {
        var options = new ProfilingOptions { Enabled = true, Operations = new() { Enabled = true } };
        configure?.Invoke(options.Operations);
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 8, 10, 0, 0, TimeSpan.Zero));
        var real = new InMemoryProfilingStorageProvider(options, clock);
        var queue = new OperationProfilingCompletionQueue(options, clock);
        var nodes = new ProfilingNodeIdentityProvider(clock);
        var profiler = new OperationProfiler(options, nodes, queue, clock);
        var health = new OperationProfilingHealthState(queue, real, nodes, options, profiler);
        var store = Substitute.For<IOperationProfilingStore>();
        store.OpenWriterAsync(Arg.Any<ProfilingOpenWriterRequest>(), Arg.Any<CancellationToken>()).Returns(call => real.Operations.OpenWriterAsync(call.Arg<ProfilingOpenWriterRequest>(), call.Arg<CancellationToken>()));
        store.SynchronizeWriterAsync(Arg.Any<ProfilingWriterSynchronizationRequest>(), Arg.Any<CancellationToken>()).Returns(call => real.Operations.SynchronizeWriterAsync(call.Arg<ProfilingWriterSynchronizationRequest>(), call.Arg<CancellationToken>()));
        store.AcknowledgeClearAsync(Arg.Any<ProfilingClearAcknowledgement>(), Arg.Any<CancellationToken>()).Returns(call => real.Operations.AcknowledgeClearAsync(call.Arg<ProfilingClearAcknowledgement>(), call.Arg<CancellationToken>()));
        store.AppendAsync(Arg.Any<IReadOnlyList<ProfilingWriteEnvelope>>(), Arg.Any<CancellationToken>()).Returns(call => real.Operations.AppendAsync(call.Arg<IReadOnlyList<ProfilingWriteEnvelope>>(), call.Arg<CancellationToken>()));
        store.CloseWriterAsync(Arg.Any<ProfilingWriterLease>(), Arg.Any<CancellationToken>()).Returns(call => real.Operations.CloseWriterAsync(call.Arg<ProfilingWriterLease>(), call.Arg<CancellationToken>()));
        var writer = new OperationProfilingWriterService(store, queue, health, nodes, profiler, options, clock);
        return new(options, clock, real, store, queue, profiler, health, writer);
    }

    private static void Capture(Harness h, string key = "work")
    {
        using var boundary = h.Profiler.BeginExecutionBoundary();
        using var operation = h.Profiler.BeginOperation(key);
        h.Clock.Advance(TimeSpan.FromMilliseconds(1));
        operation.Complete();
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) { await Task.Delay(1, deadline.Token); }
    }

    private sealed record Harness(ProfilingOptions Options, FakeTimeProvider Clock, InMemoryProfilingStorageProvider Real,
        IOperationProfilingStore Store, OperationProfilingCompletionQueue Queue, OperationProfiler Profiler,
        OperationProfilingHealthState Health, OperationProfilingWriterService Writer);
}
