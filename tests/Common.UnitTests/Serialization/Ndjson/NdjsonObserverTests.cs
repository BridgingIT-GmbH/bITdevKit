// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Serialization;

using BridgingIT.DevKit.Testing.Ndjson;
using Microsoft.Extensions.Logging;

public class NdjsonObserverTests
{
    [Fact]
    public async Task WriteAsync_ObserverFails_ReportsSafeCodeWithoutExceptionOrPayload()
    {
        var logger = Substitute.For<ILogger>();
        using var output = new NdjsonTestStreams.RecordingStream();
        var observer = new Observer { Notification = () => throw new InvalidOperationException("Private payload must not be logged.") };
        await new NdjsonWriter(logger: logger).WriteAsync(new[] { "fictional data" }, output, observer);
        var warnings = logger.ReceivedCalls().Where(c => c.GetMethodInfo().Name == nameof(ILogger.Log)
            && c.GetArguments()[0] is LogLevel.Warning).ToArray();
        warnings.Length.ShouldBe(1);
        warnings[0].GetArguments()[2].ToString().ShouldContain("stream_observer_failed");
        warnings[0].GetArguments()[2].ToString().ShouldNotContain("Private payload");
        warnings[0].GetArguments()[2].ToString().ShouldNotContain("fictional data");
        warnings[0].GetArguments()[3].ShouldBeNull();
        observer.Count.ShouldBe(1);
        observer.Outcome.Status.ShouldBe(NdjsonStreamStatus.Succeeded);
    }

    [Fact]
    public async Task WriteAsync_FinalFlushFails_ReportsSourceExhaustionButFailedOutput()
    {
        using var output = new NdjsonTestStreams.RecordingStream { FailFlushAt = 3 };
        var observer = new Observer();
        await Should.ThrowAsync<NdjsonStreamException>(() => new NdjsonWriter().WriteAsync(new[] { 1, 2 }, output, observer));
        observer.Count.ShouldBe(1);
        observer.Outcome.SourceCompleted.ShouldBeTrue();
        observer.Outcome.CleanupCompleted.ShouldBeTrue();
        observer.Outcome.Status.ShouldBe(NdjsonStreamStatus.Failed);
        observer.Outcome.FailureStage.ShouldBe(NdjsonStreamFailureStage.Flush);
        observer.Outcome.WrittenRecordCount.ShouldBe(2);
        observer.Outcome.FlushedRecordCount.ShouldBe(2);
        observer.Outcome.PartialWritePossible.ShouldBeTrue();
    }

    [Fact]
    public async Task WriteAsync_PartialSecondWrite_KeepsOnlyConfirmedPrefixes()
    {
        using var output = new NdjsonTestStreams.RecordingStream { FailWriteAt = 2, PartialWriteBytes = 1 };
        var observer = new Observer();
        var error = await Should.ThrowAsync<NdjsonStreamException>(() => new NdjsonWriter().WriteAsync(new[] { 1, 2 }, output, observer));
        error.Message.ShouldNotContain("Payload");
        observer.Outcome.WrittenRecordCount.ShouldBe(1);
        observer.Outcome.WrittenFramedByteCount.ShouldBe(2);
        observer.Outcome.FlushedRecordCount.ShouldBe(1);
        observer.Outcome.PartialWritePossible.ShouldBeTrue();
        observer.Outcome.FailureStage.ShouldBe(NdjsonStreamFailureStage.Write);
    }

    [Fact]
    public async Task WriteAsync_DisposalDoesNotCooperate_BoundsWaitAndNeverRenotifies()
    {
        var clock = new NdjsonTestTimeProvider();
        var source = new DisposeSequence();
        using var output = new NdjsonTestStreams.RecordingStream();
        var observer = new Observer();
        var task = new NdjsonWriter(timeProvider: clock).WriteAsync(source, output, observer);
        await source.DisposalStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await clock.WaitForTimersAsync(1);
        clock.Advance(TimeSpan.FromSeconds(5));
        var error = await Should.ThrowAsync<NdjsonStreamException>(() => task.WaitAsync(TimeSpan.FromSeconds(10)));
        error.ReasonCode.ShouldBe("stream_cleanup_timeout");
        observer.Count.ShouldBe(1);
        observer.Outcome.SourceCompleted.ShouldBeTrue();
        observer.Outcome.CleanupCompleted.ShouldBeFalse();
        observer.Outcome.Status.ShouldBe(NdjsonStreamStatus.Failed);
        source.ReleaseDisposal.TrySetResult();
        await source.DisposalFinished.Task.WaitAsync(TimeSpan.FromSeconds(10));
        observer.Count.ShouldBe(1);
        source.DisposalCount.ShouldBe(1);
    }

    [Fact]
    public async Task WriteAsync_ObserverFails_DoesNotChangeSuccessfulOutput()
    {
        using var output = new NdjsonTestStreams.RecordingStream();
        var observer = new Observer { Notification = () => throw new InvalidOperationException("private details") };
        await new NdjsonWriter().WriteAsync(new[] { 1 }, output, observer);
        observer.Count.ShouldBe(1);
        observer.Outcome.Status.ShouldBe(NdjsonStreamStatus.Succeeded);
    }

    [Fact]
    public async Task WriteAsync_ObserverIgnoresBudget_ReturnsWithoutRetry()
    {
        var clock = new NdjsonTestTimeProvider();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observer = new Observer { Notification = () => completion.Task };
        using var output = new NdjsonTestStreams.RecordingStream();
        var task = new NdjsonWriter(timeProvider: clock).WriteAsync(new[] { 1 }, output, observer);
        await clock.WaitForTimersAsync(1);
        clock.Advance(TimeSpan.FromSeconds(5));
        await task.WaitAsync(TimeSpan.FromSeconds(10));
        observer.Count.ShouldBe(1);
        observer.Outcome.Status.ShouldBe(NdjsonStreamStatus.Succeeded);
        completion.TrySetResult();
    }

    private sealed class Observer : INdjsonStreamObserver
    {
        internal int Count { get; private set; }

        internal NdjsonStreamOutcome Outcome { get; private set; }

        internal Func<Task> Notification { get; init; }

        public Task OnFinishedAsync(NdjsonStreamOutcome outcome, CancellationToken cancellationToken = default)
        {
            this.Count++;
            this.Outcome = outcome;
            return this.Notification?.Invoke() ?? Task.CompletedTask;
        }
    }

    private sealed class DisposeSequence : IAsyncEnumerable<int>, IAsyncEnumerator<int>
    {
        private int moves;
        internal TaskCompletionSource DisposalStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseDisposal { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource DisposalFinished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int DisposalCount { get; private set; }

        public int Current => 1;
        public IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default) => this;
        public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(this.moves++ == 0);
        public async ValueTask DisposeAsync()
        {
            this.DisposalCount++;
            this.DisposalStarted.TrySetResult();
            await this.ReleaseDisposal.Task;
            this.DisposalFinished.TrySetResult();
        }
    }
}
