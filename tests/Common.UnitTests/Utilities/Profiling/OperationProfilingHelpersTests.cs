// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

public sealed class OperationProfilingHelpersTests
{
    [Fact]
    public async Task AsyncHelper_PreservesResult_AndRestoresAmbientBeforeCallerContinues()
    {
        // Arrange
        var (profiler, sink, _) = OperationProfilerTests.Create();
        var expected = new object();
        var calls = 0;

        // Act
        var result = await profiler.RunOperationAsync("service", async (scope, _) =>
        {
            profiler.Current.ShouldBeSameAs(scope);
            Interlocked.Increment(ref calls);
            await Task.Yield();
            profiler.Current.ShouldBeSameAs(scope);
            return expected;
        });

        // Assert
        result.ShouldBeSameAs(expected);
        calls.ShouldBe(1);
        profiler.Current.ShouldBeNull();
        sink.Records.ShouldHaveSingleItem().Outcome.ShouldBe(OperationProfilingOutcome.Completed);
        profiler.ActiveCount.ShouldBe(0);
    }

    [Fact]
    public async Task NestedHelpers_PreserveOriginalException_AndCountEachAffectedInvocationOnce()
    {
        // Arrange
        var (profiler, sink, _) = OperationProfilerTests.Create();
        var expected = new InvalidOperationException("sensitive payload");
        var calls = 0;

        // Act
        var actual = await Should.ThrowAsync<InvalidOperationException>(() => profiler.RunOperationAsync("root", async (_, token) =>
            await profiler.RunSegmentAsync<int>("child", async (_, _) =>
            {
                calls++;
                await Task.Yield();
                throw expected;
            }, token)));

        // Assert
        actual.ShouldBeSameAs(expected);
        calls.ShouldBe(1);
        var record = sink.Records.ShouldHaveSingleItem();
        record.Outcome.ShouldBe(OperationProfilingOutcome.Failed);
        record.Failure.Message.ShouldBeNull();
        record.Segments.ShouldHaveSingleItem().Outcomes.ShouldHaveSingleItem().Outcome.ShouldBe(ProfilingSegmentOutcome.Failed);
        record.Segments.Single().Statistics.Count.ShouldBe(1);
        profiler.Current.ShouldBeNull();
        profiler.ActiveBytes.ShouldBe(0);
    }

    [Theory]
    [InlineData(true, true, OperationProfilingOutcome.Canceled)]
    [InlineData(true, false, OperationProfilingOutcome.Failed)]
    [InlineData(false, true, OperationProfilingOutcome.Failed)]
    public async Task Cancellation_ClassifiesOnlyCanceledMatchingApplicationToken(bool canceled, bool matching, OperationProfilingOutcome outcome)
    {
        // Arrange
        var (profiler, sink, _) = OperationProfilerTests.Create();
        using var application = new CancellationTokenSource();
        using var other = new CancellationTokenSource();
        if (canceled)
        {
            application.Cancel();
        }

        var expected = new OperationCanceledException(matching ? application.Token : other.Token);

        // Act
        OperationCanceledException actual = null;
        try
        {
            await profiler.RunOperationAsync<int>("cancel", (_, _) => Task.FromException<int>(expected), application.Token);
        }
        catch (OperationCanceledException exception)
        {
            actual = exception;
        }

        // Assert
        actual.ShouldBeSameAs(expected);
        sink.Records.ShouldHaveSingleItem().Outcome.ShouldBe(outcome);
        profiler.Current.ShouldBeNull();
    }

    [Fact]
    public void SynchronousHelper_PreservesExceptionIdentity_WithoutInferringCancellation()
    {
        // Arrange
        var (profiler, sink, _) = OperationProfilerTests.Create();
        var expected = new OperationCanceledException();
        var calls = 0;

        // Act
        var actual = Should.Throw<OperationCanceledException>(() => profiler.RunOperation<int>("sync", _ => { calls++; throw expected; }));

        // Assert
        actual.ShouldBeSameAs(expected);
        calls.ShouldBe(1);
        sink.Records.ShouldHaveSingleItem().Outcome.ShouldBe(OperationProfilingOutcome.Failed);
        profiler.Current.ShouldBeNull();
    }

    [Fact]
    public async Task ResultFailure_RequiresExplicitClassifier_AndReturnsSameResult()
    {
        // Arrange
        var (profiler, sink, _) = OperationProfilerTests.Create();
        var failure = Result<int>.Failure("business failure");

        // Act
        var unchanged = await profiler.RunOperationAsync("classified", (_, _) => Task.FromResult(failure),
            classify: value => OperationProfilingHelpers.ClassifyResult(value));
        await profiler.RunOperationAsync("unclassified", (_, _) => Task.FromResult(failure));

        // Assert
        unchanged.ShouldBe(failure);
        var records = sink.Records.ToArray();
        records[0].Outcome.ShouldBe(OperationProfilingOutcome.Failed);
        records[0].Failure.Source.ShouldBe("Result");
        records[0].Failure.Message.ShouldBeNull();
        records[1].Outcome.ShouldBe(OperationProfilingOutcome.Completed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BrokenClassifier_PreservesBusinessValue_AndReportsIncomplete(bool throws)
    {
        // Arrange
        var (profiler, sink, _) = OperationProfilerTests.Create();
        var calls = 0;

        // Act
        var result = await profiler.RunOperationAsync("classify", (_, _) => { calls++; return Task.FromResult(42); },
            classify: _ => throws ? throw new InvalidOperationException("classifier") : null);

        // Assert
        result.ShouldBe(42);
        calls.ShouldBe(1);
        var record = sink.Records.ShouldHaveSingleItem();
        record.Outcome.ShouldBe(OperationProfilingOutcome.Incomplete);
        record.Quality.ClassificationFailed.ShouldBeTrue();
        record.Quality.IncompleteReason.ShouldBe(ProfilingIncompleteReason.ClassificationFailed);
    }

    [Fact]
    public async Task OptionalOrThrowingProfiler_InvokesBusinessExactlyOnce()
    {
        // Arrange
        var faulting = Substitute.For<IOperationProfiler>();
        faulting.BeginOperation(Arg.Any<string>(), Arg.Any<OperationProfilingKind>())
            .Returns(_ => throw new InvalidOperationException("observer"));
        var calls = 0;

        // Act
        var first = await ((IOperationProfiler)null).RunOperationAsync("absent", (_, _) => { calls++; return Task.FromResult(10); });
        var second = await faulting.RunOperationAsync("fault", (_, _) => { calls++; return Task.FromResult(20); });

        // Assert
        first.ShouldBe(10);
        second.ShouldBe(20);
        calls.ShouldBe(2);
    }

    [Fact]
    public async Task JoinOrStart_RespectsSuppressedAndRejectedBoundaries()
    {
        // Arrange
        var (profiler, sink, _) = OperationProfilerTests.Create(o => o.MaxActiveOperations = 1);
        using var root = profiler.BeginOperation("root");

        // Act
        using (var rejected = profiler.BeginOperation("rejected"))
        {
            await profiler.JoinOrStartAsync("nested", OperationProfilingKind.Job, (_, _) => Task.FromResult(1));
        }

        using (profiler.Suppress())
        {
            await profiler.JoinOrStartAsync("excluded", OperationProfilingKind.Job, (_, _) => Task.FromResult(2));
        }

        await profiler.JoinOrStartAsync("joined", OperationProfilingKind.Service, (_, _) => Task.FromResult(3));
        profiler.Current.ShouldBeSameAs(root);
        root.Complete();
        root.Dispose();

        // Assert
        sink.Records.ShouldHaveSingleItem().Segments.ShouldHaveSingleItem().Key.ShouldBe("joined");
    }

    [Fact]
    public async Task ConcurrentIndependentActions_KeepKeysAndAmbientStateIsolated()
    {
        // Arrange
        var (profiler, sink, _) = OperationProfilerTests.Create();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = 0;

        // Act
        var actions = Enumerable.Range(0, 8).Select(i => profiler.RunOperationAsync($"action:{i}", OperationProfilingKind.BlazorInteraction, async (scope, _) =>
        {
            scope.SetDimension("action", i);
            if (Interlocked.Increment(ref started) == 8)
            {
                release.SetResult();
            }

            await release.Task;
            profiler.Current.ShouldBeSameAs(scope);
            return i;
        })).ToArray();
        var results = await Task.WhenAll(actions);

        // Assert
        results.ShouldBe(Enumerable.Range(0, 8));
        profiler.Current.ShouldBeNull();
        sink.Records.Count.ShouldBe(8);
        sink.Records.Select(r => r.Id).Distinct().Count().ShouldBe(8);
        sink.Records.All(r => r.Segments.Count == 0).ShouldBeTrue();
        profiler.ActiveBytes.ShouldBe(0);
    }

    [Fact]
    public async Task ValueTaskAdapter_AwaitsBusinessOnce_AndRestoresOuterContext()
    {
        // Arrange
        var (profiler, sink, _) = OperationProfilerTests.Create();
        using var outer = profiler.BeginOperation("outer");
        var calls = 0;

        // Act
        var result = await OperationProfilingHelpers.RunSegmentValueTaskAsync(profiler, "step", async (_, _) =>
        {
            calls++;
            await Task.Yield();
            return 42;
        }, default);
        profiler.Current.ShouldBeSameAs(outer);
        outer.Complete();
        outer.Dispose();

        // Assert
        result.ShouldBe(42);
        calls.ShouldBe(1);
        sink.Records.ShouldHaveSingleItem().Segments.ShouldHaveSingleItem().Statistics.Count.ShouldBe(1);
    }
}
