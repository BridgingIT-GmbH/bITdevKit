// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Ndjson;

using System.Runtime.CompilerServices;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Testing.Ndjson;

public class NdjsonHttpObserverTests
{
    [Fact]
    public async Task ExecuteAsync_HeaderStartDeadline_DoesNotMoveProducer()
    {
        var clock = new NdjsonTestTimeProvider();
        using var output = new NdjsonTestStreams.RecordingStream();
        using var services = NdjsonHttpResultTests.Services(clock);
        var (context, body, lifetime) = NdjsonHttpResultTests.Context(services, output);
        body.StartGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task;
        var observer = new NdjsonHttpResultTests.OutcomeObserver();
        var moved = false;
        var task = NdjsonHttpResult.From(Source()).WithObserver(observer).ExecuteAsync(context);
        await body.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await clock.WaitForTimersAsync(2);
        clock.Advance(TimeSpan.FromSeconds(30));
        await task.WaitAsync(TimeSpan.FromSeconds(10));
        moved.ShouldBeFalse();
        lifetime.Aborted.ShouldBeTrue();
        observer.Outcome.ReasonCode.ShouldBe("stream_write_timeout");
        observer.Outcome.FailureStage.ShouldBe(NdjsonStreamFailureStage.Write);
        observer.Count.ShouldBe(1);

        IEnumerable<int> Source() { moved = true; yield return 1; }
    }

    [Fact]
    public async Task ExecuteAsync_CanceledPendingMove_UsesOneCleanupBudgetAndDisposesOnlyAfterLateSettlement()
    {
        var clock = new NdjsonTestTimeProvider();
        var source = new SlowMove();
        using var output = new NdjsonTestStreams.RecordingStream();
        using var services = NdjsonHttpResultTests.Services(clock);
        var (context, _, lifetime) = NdjsonHttpResultTests.Context(services, output);
        var observer = new NdjsonHttpResultTests.OutcomeObserver();
        var task = NdjsonHttpResult.From(source).WithObserver(observer).ExecuteAsync(context);
        await source.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        lifetime.Abort();
        await clock.WaitForTimersAsync(2);
        clock.Advance(TimeSpan.FromSeconds(5));
        await task.WaitAsync(TimeSpan.FromSeconds(10));
        source.Disposals.ShouldBe(0);
        observer.Outcome.Status.ShouldBe(NdjsonStreamStatus.Failed);
        observer.Outcome.ReasonCode.ShouldBe("stream_cleanup_timeout");
        observer.Outcome.CleanupCompleted.ShouldBeFalse();
        lifetime.Aborted.ShouldBeTrue();
        source.Gate.TrySetResult(false);
        await source.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        source.Disposals.ShouldBe(1);
        observer.Count.ShouldBe(1);
        output.WriteCount.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_DurationExpires_UsesFreshTerminalBudgetAndCleansProducer()
    {
        var clock = new NdjsonTestTimeProvider();
        using var output = new NdjsonTestStreams.RecordingStream();
        using var services = NdjsonHttpResultTests.Services(clock);
        var (context, _, _) = NdjsonHttpResultTests.Context(services, output);
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observer = new NdjsonHttpResultTests.OutcomeObserver();
        var task = NdjsonHttpResult.From(Source(), new() { Mode = NdjsonMode.Envelope, MaxDuration = TimeSpan.FromSeconds(1) }).WithObserver(observer).ExecuteAsync(context);
        await clock.WaitForTimersAsync(4);
        clock.Advance(TimeSpan.FromSeconds(1));
        await released.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await task.WaitAsync(TimeSpan.FromSeconds(10));
        observer.Outcome.Status.ShouldBe(NdjsonStreamStatus.Failed);
        observer.Outcome.ReasonCode.ShouldBe("stream_duration_exceeded");
        observer.Outcome.CleanupCompleted.ShouldBeTrue();
        Encoding.UTF8.GetString(output.Bytes).ShouldContain("\"kind\":\"error\"");
        Encoding.UTF8.GetString(output.Bytes).ShouldNotContain("completed");

        async IAsyncEnumerable<int> Source([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
            finally { released.TrySetResult(); }

            yield break;
        }
    }

    [Fact]
    public async Task ExecuteAsync_NonCooperativeWrite_AbortsWithoutRetryAndRetainsConfirmedCounts()
    {
        var clock = new NdjsonTestTimeProvider();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var output = new NdjsonTestStreams.RecordingStream { WriteGate = gate.Task, IgnoreCancellation = true };
        using var services = NdjsonHttpResultTests.Services(clock);
        var (context, _, lifetime) = NdjsonHttpResultTests.Context(services, output);
        var observer = new NdjsonHttpResultTests.OutcomeObserver();
        var task = NdjsonHttpResult.From(new[] { 1 }).WithObserver(observer).ExecuteAsync(context);
        await output.WriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await clock.WaitForTimersAsync(3);
        clock.Advance(TimeSpan.FromSeconds(30));
        await lifetime.AbortedSignal.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await clock.WaitForTimersAsync(4);
        clock.Advance(TimeSpan.FromSeconds(5));
        await task.WaitAsync(TimeSpan.FromSeconds(10));
        observer.Outcome.Status.ShouldBe(NdjsonStreamStatus.Failed);
        observer.Outcome.ReasonCode.ShouldBe("stream_write_timeout");
        observer.Outcome.WrittenRecordCount.ShouldBe(0);
        observer.Outcome.FlushedRecordCount.ShouldBe(0);
        observer.Outcome.PartialWritePossible.ShouldBeTrue();
        observer.Outcome.CleanupCompleted.ShouldBeFalse();
        output.WriteCount.ShouldBe(1);
        gate.TrySetResult();
        await output.WriteFinished.Task.WaitAsync(TimeSpan.FromSeconds(10));
        observer.Count.ShouldBe(1);
        observer.Outcome.WrittenRecordCount.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_CleanupTimesOut_NeverEmitsSuccessfulCompletion()
    {
        var clock = new NdjsonTestTimeProvider();
        var source = new SlowCleanup();
        using var output = new NdjsonTestStreams.RecordingStream();
        using var services = NdjsonHttpResultTests.Services(clock);
        var (context, _, lifetime) = NdjsonHttpResultTests.Context(services, output);
        var observer = new NdjsonHttpResultTests.OutcomeObserver();
        var task = NdjsonHttpResult.From(source, new() { Mode = NdjsonMode.Envelope }).WithObserver(observer).ExecuteAsync(context);
        await source.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await clock.WaitForTimersAsync(4);
        clock.Advance(TimeSpan.FromSeconds(5));
        await task.WaitAsync(TimeSpan.FromSeconds(10));
        observer.Outcome.SourceCompleted.ShouldBeTrue();
        observer.Outcome.CleanupCompleted.ShouldBeFalse();
        observer.Outcome.ReasonCode.ShouldBe("stream_cleanup_timeout");
        lifetime.Aborted.ShouldBeTrue();
        Encoding.UTF8.GetString(output.Bytes).ShouldNotContain("completed");
        source.Gate.TrySetResult();
        await source.Finished.Task.WaitAsync(TimeSpan.FromSeconds(10));
        observer.Count.ShouldBe(1);
    }

    private sealed class SlowMove : IAsyncEnumerable<int>, IAsyncEnumerator<int>
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<bool> Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Disposals { get; private set; }

        public int Current => 1;
        public IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default) => this;
        public ValueTask<bool> MoveNextAsync() { this.Started.TrySetResult(); return new(this.Gate.Task); }

        public ValueTask DisposeAsync() { this.Gate.Task.IsCompleted.ShouldBeTrue(); this.Disposals++; this.Disposed.TrySetResult(); return ValueTask.CompletedTask; }
    }

    private sealed class SlowCleanup : IAsyncEnumerable<int>, IAsyncEnumerator<int>
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Current => 0;
        public IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default) => this;
        public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(false);
        public async ValueTask DisposeAsync() { this.Started.TrySetResult(); await this.Gate.Task; this.Finished.TrySetResult(); }
    }
}
