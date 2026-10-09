// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

/// <summary>Verifies shared view admission, deadlines and aggregate arithmetic.</summary>
public sealed class OperationProfilingQueryTests
{
    /// <summary>Explicitly larger selections apply the same bound to root and segment distributions.</summary>
    /// <example>Run with the Analysis_ConfiguredLargerSelection filter.</example>
    [Fact]
    public async Task Analysis_ConfiguredLargerSelection_ComputesEveryOwnerAndRejectsOverflow()
    {
        var (profiler, sink, clock) = OperationProfilerTests.Create();
        using (var operation = profiler.BeginOperation("Report"))
        {
            operation.RunSegment("Read", _ => clock.Advance(TimeSpan.FromMilliseconds(2)));
            operation.Complete();
        }

        var record = sink.Records.Single();
        var selection = new OperationProfilingAnalysisSelection
        {
            Records = Enumerable.Range(0, 10001).Select(_ => record with { Id = Guid.NewGuid() }).ToArray(),
        };
        var query = new OperationProfilingQuery { MaximumAnalysisCount = 20000 };

        var sut = OperationProfilingAnalysis.Create(selection, query);

        sut.Count.ShouldBe(10001);
        var segment = sut.Segments.ShouldHaveSingleItem();
        segment.OperationCount.ShouldBe(10001);
        segment.InvocationCount.ShouldBe(10001);
        segment.TotalPerOperation.Count.ShouldBe(10001);
        segment.SelfPerOperation.Count.ShouldBe(10001);
        segment.TotalPerOperation.P50Milliseconds.ShouldBe(2);
        segment.SelfPerOperation.P95Milliseconds.ShouldBe(2);
        segment.InvocationMeanMilliseconds.ShouldBe(2);
        Should.Throw<ArgumentException>(() => OperationProfilingAnalysis.Create(selection, query with { MaximumAnalysisCount = 10000 }));

        var store = Substitute.For<IOperationProfilingStore>();
        store.SelectAnalysisAsync(Arg.Any<OperationProfilingQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IResult<OperationProfilingAnalysisSelection>>(Result<OperationProfilingAnalysisSelection>.Success(selection)));
        var provider = Substitute.For<IProfilingStorageProvider>();
        provider.Operations.Returns(store);
        var options = new ProfilingOptions { Enabled = true };
        options.Queries.MaximumAnalysisRecords = 20000;
        var facade = new OperationProfilingQueryService(provider, options, null);
        var accepted = await facade.AnalyzeAsync(query);
        accepted.IsSuccess.ShouldBeTrue();
        accepted.Value.Segments.Single().TotalPerOperation.Count.ShouldBe(10001);
        (await facade.AnalyzeAsync(query with { MaximumAnalysisCount = 10000 })).Errors.ShouldContain(error => error is ProfilingQueryLimitError);
    }

    /// <summary>Cancellation-ignoring provider calls continue occupying their original admission until unwind.</summary>
    [Fact]
    public async Task TimedOutProvider_RetainsCapacity_AndDiscardsLateResponse()
    {
        var store = Substitute.For<IOperationProfilingStore>();
        var pending = new TaskCompletionSource<IResult<OperationProfilingPage>>(TaskCreationOptions.RunContinuationsAsynchronously);
        store.QueryAsync(Arg.Any<OperationProfilingQuery>(), Arg.Any<CancellationToken>()).Returns(pending.Task);
        var sut = Create(store, TimeSpan.FromMilliseconds(40));

        var timeout = await sut.QueryAsync(new());
        var busy = await sut.QueryAsync(new());
        timeout.Errors.ShouldContain(error => error is ProfilingQueryTimeoutError);
        busy.Errors.ShouldContain(error => error is ProfilingBusyError);
        pending.SetResult(Result<OperationProfilingPage>.Success(new()));
        await WaitForCapacity(sut);
    }

    /// <summary>A view shares admission across sequential subqueries and closes escaped references.</summary>
    [Fact]
    public async Task View_UsesOneAdmission_AndRejectsEscapedSession()
    {
        var store = Substitute.For<IOperationProfilingStore>();
        store.QueryAsync(Arg.Any<OperationProfilingQuery>(), Arg.Any<CancellationToken>()).Returns(Result<OperationProfilingPage>.Success(new()));
        var sut = Create(store, TimeSpan.FromSeconds(2));
        IOperationProfilingQueryService escaped = null;
        var result = await sut.BuildViewAsync(async (session, token) =>
        {
            escaped = session;
            (await session.QueryAsync(new(), token)).IsSuccess.ShouldBeTrue();
            (await session.QueryAsync(new(), token)).IsSuccess.ShouldBeTrue();
            return Result<int>.Success(42) as IResult<int>;
        });
        result.Value.ShouldBe(42);
        (await escaped.QueryAsync(new())).Errors.ShouldContain(error => error is ProfilingQueryTimeoutError);
        await store.Received(2).QueryAsync(Arg.Any<OperationProfilingQuery>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Unawaited subordinate work remains accounted for even after the callback returns.</summary>
    [Fact]
    public async Task View_UnawaitedSubordinate_RetainsAdmission()
    {
        var store = Substitute.For<IOperationProfilingStore>();
        var pending = new TaskCompletionSource<IResult<OperationProfilingPage>>(TaskCreationOptions.RunContinuationsAsynchronously);
        store.QueryAsync(Arg.Any<OperationProfilingQuery>(), Arg.Any<CancellationToken>()).Returns(pending.Task);
        var sut = Create(store, TimeSpan.FromSeconds(2));
        var result = await sut.BuildViewAsync((session, token) =>
        {
            _ = session.QueryAsync(new(), token);
            return Task.FromResult<IResult<int>>(Result<int>.Success(42));
        });
        result.Value.ShouldBe(42);
        (await sut.QueryAsync(new())).Errors.ShouldContain(error => error is ProfilingBusyError);
        pending.SetResult(Result<OperationProfilingPage>.Success(new()));
        await WaitForCapacity(sut);
    }

    /// <summary>Caller cancellation propagates while provider errors become safe unavailable results.</summary>
    [Fact]
    public async Task CallerCancellation_AndProviderFault_AreDistinct()
    {
        var store = Substitute.For<IOperationProfilingStore>();
        store.QueryAsync(Arg.Any<OperationProfilingQuery>(), Arg.Any<CancellationToken>()).Returns(_ => Task.FromException<IResult<OperationProfilingPage>>(new InvalidOperationException("private backend error")));
        var sut = Create(store, TimeSpan.FromSeconds(2));
        (await sut.QueryAsync(new())).Errors.ShouldContain(error => error is ProfilingUnavailableError);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => sut.QueryAsync(new(), canceled.Token));
    }

    /// <summary>Exact statistics use midpoint medians and invocation weighting rather than averages of averages.</summary>
    [Fact]
    public void Analysis_WeightsInvocationMeans_AndUsesOwnerPercentiles()
    {
        var (profiler, sink, clock) = OperationProfilerTests.Create();
        for (var owner = 0; owner < 2; owner++)
        {
            using var operation = profiler.BeginOperation(owner == 0 ? "Report" : "report");
            for (var invocation = 0; invocation < (owner == 0 ? 1 : 3); invocation++)
            { operation.RunSegment("Load", _ => clock.Advance(TimeSpan.FromMilliseconds(owner == 0 ? 100 : 10))); }

            operation.Complete();
        }

        var sut = OperationProfilingAnalysis.Create(new() { Records = sink.Records.ToArray() }, new());
        sut.Count.ShouldBe(2);
        sut.P50Milliseconds.ShouldBe(65);
        sut.P95Milliseconds.ShouldBe(100);
        var segment = sut.Segments.Single();
        segment.InvocationCount.ShouldBe(4);
        segment.InvocationMeanMilliseconds.ShouldBe(32.5);
        segment.TotalPerOperation.P50Milliseconds.ShouldBe(65);
        segment.MinimumInvocationMilliseconds.ShouldBe(10);
        segment.MaximumInvocationMilliseconds.ShouldBe(100);
    }

    /// <summary>Public aggregate entrypoints reject oversized selections instead of truncating them.</summary>
    [Fact]
    public void Analysis_RejectsOversizedSelection()
    {
        Should.Throw<ArgumentException>(() => OperationProfilingAnalysis.Distribution(Enumerable.Repeat(TimeSpan.Zero, 3), 2));
        Should.Throw<ArgumentException>(() => OperationProfilingAnalysis.Create(new() { Records = new OperationProfilingRecord[3] }, new() { MaximumAnalysisCount = 2 }));
    }

    /// <summary>Measurement averages use sample sums and counts and sampling remains an observed policy label.</summary>
    [Fact]
    public void Analysis_ReducesCompatibleMeasurements_AndLabelsSampling()
    {
        var (profiler, sink, clock) = OperationProfilerTests.Create();
        for (var owner = 0; owner < 2; owner++)
        {
            using var operation = profiler.BeginOperation("Report");
            operation.SetHttpMetadata(new() { SamplingStrategyKey = "Probability", SamplingConfigurationKey = "p=0.1", SamplingInclusionProbability = .1 });
            for (var invocation = 0; invocation < (owner == 0 ? 1 : 3); invocation++)
            {
                operation.RunSegment("Load", segment =>
                {
                    segment.SetMeasurement("items", owner == 0 ? 100 : 10, "count", MeasurementAggregation.Average);
                    segment.SetDimension("source", owner == 0 ? "one" : "two");
                    clock.Advance(TimeSpan.FromMilliseconds(1));
                });
            }

            operation.Complete();
        }

        var sut = OperationProfilingAnalysis.Create(new() { Records = sink.Records.ToArray() }, new());
        var measurement = sut.Segments.Single().Measurements.Single();
        measurement.SampleCount.ShouldBe(4);
        measurement.Value.Scalar.ShouldBe("32.5");
        measurement.Sum.Scalar.ShouldBe("130");
        sut.Segments.Single().Dimensions.Single().Mixed.ShouldBeTrue();
        sut.Sampling.Single().Count.ShouldBe(2);
        sut.Sampling.Single().MinimumInclusionProbability.ShouldBe(.1);
        sut.Count.ShouldBe(2); // Counts are never scaled by inclusion probability.
    }

    /// <summary>Serving-process counters remain distinct from shared retained counts.</summary>
    [Fact]
    public async Task Health_DoesNotTurnLocalCountersIntoSharedTotals()
    {
        var store = Substitute.For<IOperationProfilingStore>();
        store.QueryAsync(Arg.Any<OperationProfilingQuery>(), Arg.Any<CancellationToken>()).Returns(Result<OperationProfilingPage>.Success(new() { TotalCount = 200 }));
        var provider = Substitute.For<IProfilingStorageProvider>();
        provider.Operations.Returns(store);
        var node = Guid.NewGuid();
        var health = Substitute.For<IOperationProfilingHealthSource>();
        health.GetSnapshot().Returns(new OperationProfilingHealth { CountersNodeId = node, ProviderShared = true, PersistedOperations = 2, QueueRecords = 3 });
        var sut = new OperationProfilingQueryService(provider, new() { Enabled = true }, health);
        (await sut.QueryAsync(new())).Value.TotalCount.ShouldBe(200);
        var observed = sut.GetHealth();
        observed.CountersNodeId.ShouldBe(node);
        observed.ProviderShared.ShouldBeTrue();
        observed.PersistedOperations.ShouldBe(2);
        observed.QueueRecords.ShouldBe(3);
    }

    /// <summary>Synchronous provider work cannot block the caller past its deadline or release occupied capacity early.</summary>
    [Fact]
    public async Task BlockingProvider_ReturnsTimeout_WhileOriginalWorkRetainsAdmission()
    {
        var store = Substitute.For<IOperationProfilingStore>();
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.QueryAsync(Arg.Any<OperationProfilingQuery>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            entered.TrySetResult();
            release.Wait(TimeSpan.FromSeconds(5));
            return Task.FromResult<IResult<OperationProfilingPage>>(Result<OperationProfilingPage>.Success(new()));
        });
        var sut = Create(store, TimeSpan.FromMilliseconds(100));
        try
        {
            var pending = sut.QueryAsync(new());
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            (await pending).Errors.ShouldContain(error => error is ProfilingQueryTimeoutError);
            (await sut.QueryAsync(new())).Errors.ShouldContain(error => error is ProfilingBusyError);
        }
        finally { release.Set(); }

        await WaitForCapacity(sut);
    }

    private static OperationProfilingQueryService Create(IOperationProfilingStore store, TimeSpan timeout)
    {
        var provider = Substitute.For<IProfilingStorageProvider>();
        provider.Operations.Returns(store);
        var options = new ProfilingOptions { Enabled = true };
        options.Queries.MaximumConcurrentQueries = 1;
        options.Queries.Timeout = timeout;
        var health = Substitute.For<IOperationProfilingHealthSource>();
        health.GetSnapshot().Returns(new OperationProfilingHealth { ProviderScope = "NodeLocal", CountersNodeId = Guid.NewGuid() });
        return new(provider, options, health);
    }

    private static async Task WaitForCapacity(IOperationProfilingQueryService sut)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var result = await sut.QueryAsync(new());
            if (result.IsSuccess) { return; }

            await Task.Delay(5);
        }

        throw new InvalidOperationException("The completed query did not release admission.");
    }
}
