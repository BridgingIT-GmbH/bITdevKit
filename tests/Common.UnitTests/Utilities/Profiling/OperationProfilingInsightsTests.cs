// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by the LICENSE file.

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

/// <summary>Verifies exact hotspot statistics and outcome separation independently of dashboard row limits.</summary>
public sealed class OperationProfilingInsightsTests
{
    /// <summary>Only completed durations contribute to latency.</summary>
    /// <example><code>suite.Create_MixedOutcomes_SeparatesCompletedLatencyAndRootFailures();</code></example>
    [Fact]
    public void Create_MixedOutcomes_SeparatesCompletedLatencyAndRootFailures()
    {
        var records = new[] { Record(10), Record(30), Record(1000, OperationProfilingOutcome.Failed), Record(10000, OperationProfilingOutcome.Incomplete) };
        var result = OperationProfilingInsightsAnalysis.Create(new() { Records = records }, new() { Outcomes = [], PageSize = 1 });

        result.Count.ShouldBe(4);
        result.PartialCount.ShouldBe(1);
        var group = result.Groups.ShouldHaveSingleItem();
        group.Outcomes[OperationProfilingOutcome.Failed].ShouldBe(1);
        group.Outcomes[OperationProfilingOutcome.Incomplete].ShouldBe(1);
        group.CompletedLatency.Count.ShouldBe(2);
        group.CompletedLatency.P50Milliseconds.ShouldBe(20);
        group.CompletedLatency.P95Milliseconds.ShouldBe(30);
        group.CompletedElapsedMilliseconds.ShouldBe(40);
        group.RepresentativeId.ShouldBe(records[1].Id);
    }

    /// <summary>Kind, key and HTTP method use case-insensitive identities.</summary>
    /// <example><code>suite.Create_CanonicalKeysAndHttpMethods_GroupsLogicalOperationsWithoutCombiningMethods();</code></example>
    [Fact]
    public void Create_CanonicalKeysAndHttpMethods_GroupsLogicalOperationsWithoutCombiningMethods()
    {
        var records = new[] { Record(10, key: "/cities", method: "GET"), Record(20, key: "/CITIES", method: "get"), Record(30, key: "/cities", method: "POST"), Record(40, key: "/cities") };
        var result = OperationProfilingInsightsAnalysis.Create(new() { Records = records }, new() { Outcomes = [] });

        result.Groups.Count.ShouldBe(3);
        result.Groups.Single(group => group.Count == 2).CompletedLatency.P50Milliseconds.ShouldBe(15);
        result.Groups.Single(group => group.HttpMethod == "POST").Count.ShouldBe(1);
        result.Groups.Single(group => group.Kind == "Job").Count.ShouldBe(1);
    }

    /// <summary>Repeated calls retain invocation and owning-execution counts.</summary>
    /// <example><code>suite.Create_RepeatedSegments_UsesInvocationWeightedSelfTimeAndOwnerCoverage();</code></example>
    [Fact]
    public void Create_RepeatedSegments_UsesInvocationWeightedSelfTimeAndOwnerCoverage()
    {
        var segment = new ProfilingSegmentSummary { Key = "Read", Path = new(["Read"]), Statistics = new() { Count = 2, TotalDuration = TimeSpan.FromMilliseconds(8), TotalSelfDuration = TimeSpan.FromMilliseconds(6) } };
        var records = new[] { Record(10) with { Segments = [segment] }, Record(20) with { Segments = [segment] }, Record(30), Record(500, OperationProfilingOutcome.Failed) with { Segments = [segment] } };
        var group = OperationProfilingInsightsAnalysis.Create(new() { Records = records }, new() { Outcomes = [] }).Groups.ShouldHaveSingleItem();
        var analysis = group.Segments.ShouldHaveSingleItem();

        group.CompletedLatency.Count.ShouldBe(3);
        analysis.OperationCount.ShouldBe(2);
        analysis.InvocationCount.ShouldBe(4);
        analysis.InvocationSelfMeanMilliseconds.ShouldBe(3);
        analysis.SelfPerOperation.MeanMilliseconds.ShouldBe(6);
        group.CompletedElapsedMilliseconds.ShouldBe(60);
    }

    /// <summary>Failed segments do not turn successful roots into failures.</summary>
    /// <example><code>suite.Create_FailedSegments_DoNotChangeSuccessfulRootCounts();</code></example>
    [Fact]
    public void Create_FailedSegments_DoNotChangeSuccessfulRootCounts()
    {
        var record = Record(10) with { Quality = new() { Truncated = true }, Segments = [new() { Key = "Read", Path = new(["Read"]), Outcomes = [new() { Outcome = ProfilingSegmentOutcome.Failed, Statistics = new() { Count = 1 } }] }] };
        var group = OperationProfilingInsightsAnalysis.Create(new() { Records = [record] }, new() { Outcomes = [] }).Groups.ShouldHaveSingleItem();

        group.Outcomes.GetValueOrDefault(OperationProfilingOutcome.Failed).ShouldBe(0);
        group.SegmentFailureCount.ShouldBe(1);
        group.PartialCount.ShouldBe(1);
        group.CompletedLatency.Count.ShouldBe(1);
    }

    /// <summary>Missing completed observations keep latency unavailable.</summary>
    /// <example><code>suite.Create_NoCompletedObservations_ReportsUnavailableLatencyAndARealRepresentative();</code></example>
    [Fact]
    public void Create_NoCompletedObservations_ReportsUnavailableLatencyAndARealRepresentative()
    {
        var record = Record(500, OperationProfilingOutcome.Canceled);
        var group = OperationProfilingInsightsAnalysis.Create(new() { Records = [record] }, new() { Outcomes = [] }).Groups.ShouldHaveSingleItem();

        group.CompletedLatency.DurationUnavailable.ShouldBeTrue();
        group.CompletedLatency.Count.ShouldBe(0);
        group.CompletedElapsedMilliseconds.ShouldBe(0);
        group.Segments.ShouldBeEmpty();
        group.RepresentativeId.ShouldBe(record.Id);
    }

    /// <summary>Exceeded analysis bounds reject partial rankings.</summary>
    /// <example><code>suite.Create_ExceededBound_RejectsInsteadOfRankingAPartialSelection();</code></example>
    [Fact]
    public void Create_ExceededBound_RejectsInsteadOfRankingAPartialSelection()
    {
        Should.Throw<ArgumentException>(() => OperationProfilingInsightsAnalysis.Create(new() { Records = [Record(10), Record(20)] }, new() { MaximumAnalysisCount = 1 }));
    }

    /// <summary>The query service honors configured bounds and rejects invalid filters before reading the provider.</summary>
    /// <example><code>await suite.InsightsAsync_QueryBudgetsAndValidation_RemainShared();</code></example>
    [Fact]
    public async Task InsightsAsync_QueryBudgetsAndValidation_RemainShared()
    {
        var store = Substitute.For<IOperationProfilingStore>();
        store.SelectAnalysisAsync(Arg.Any<OperationProfilingQuery>(), Arg.Any<CancellationToken>()).Returns(Result<OperationProfilingAnalysisSelection>.Success(new() { Records = [Record(10), Record(20)] }));
        var provider = Substitute.For<IProfilingStorageProvider>(); provider.Operations.Returns(store);
        var health = Substitute.For<IOperationProfilingHealthSource>(); health.GetSnapshot().Returns(new OperationProfilingHealth());
        var options = new ProfilingOptions { Enabled = true }; options.Queries.MaximumAnalysisRecords = 1;
        var sut = new OperationProfilingQueryService(provider, options, health);

        (await sut.InsightsAsync(new() { MaximumAnalysisCount = 1 })).Errors.ShouldContain(error => error is ProfilingQueryLimitError);
        store.ClearReceivedCalls();
        (await sut.InsightsAsync(new() { MaximumAnalysisCount = 1, FromUtc = DateTimeOffset.UtcNow, ToUtc = DateTimeOffset.UtcNow.AddHours(-1) })).IsFailure.ShouldBeTrue();
        await store.DidNotReceive().SelectAnalysisAsync(Arg.Any<OperationProfilingQuery>(), Arg.Any<CancellationToken>());
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => sut.InsightsAsync(new(), cancellation.Token));
    }

    private static OperationProfilingRecord Record(double milliseconds, OperationProfilingOutcome outcome = OperationProfilingOutcome.Completed, string key = "job:ingestion", string method = null) => new()
    {
        Id = Guid.NewGuid(), Key = key, Kind = method is null ? "Job" : "HttpRequest", Duration = TimeSpan.FromMilliseconds(milliseconds), Outcome = outcome,
        Http = method is null ? null : new() { Method = method },
    };
}
