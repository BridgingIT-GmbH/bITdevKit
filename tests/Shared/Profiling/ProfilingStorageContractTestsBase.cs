// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Tests.Profiling;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;

// Linked into each backend test assembly; only backend creation varies.
public abstract partial class ProfilingStorageContractTestsBase
{
    protected abstract Task<IProfilingStorageProvider> CreateProviderAsync(ProfilingOptions options, TimeProvider clock);

    /// <summary>Every provider supports discovery and exact dimension-group statistics through the shared admitted query service.</summary>
    /// <example><code>await suite.DimensionQueries_AllProviders_PreserveTypedBucketsAndFullSelection();</code></example>
    [Fact]
    public async Task DimensionQueries_AllProviders_PreserveTypedBucketsAndFullSelection()
    {
        var options = new ProfilingOptions { Enabled = true };
        var h = await this.CreateAsync(options);
        var records = new[]
        {
            h.Record(durationMs: 10) with { Dimensions = [Dimension("batch", "20")] },
            h.Record(durationMs: 30) with { Dimensions = [Dimension("BATCH", "20")] },
            h.Record(durationMs: 50) with { Dimensions = [Dimension("batch", 20)] },
            h.Record(durationMs: 70) with { Dimensions = [] },
        };
        (await h.Store.AppendAsync(records.Select((record, index) => h.Envelope(record, index + 1)).ToArray())).IsSuccess.ShouldBeTrue();
        var sut = new OperationProfilingQueryService(h.Provider, options, null, clock: h.Clock);

        var discovery = await sut.DiscoverDimensionsAsync(h.Query() with { PageSize = 1 });
        discovery.IsSuccess.ShouldBeTrue();
        discovery.Value.OperationCount.ShouldBe(4);
        discovery.Value.Dimensions.Single().DistinctValueCount.ShouldBe(2);
        discovery.Value.Dimensions.Single().MissingCount.ShouldBe(1);
        var grouped = await sut.GroupAsync(h.Query() with { GroupingDimensions = ["batch"], View = OperationProfilingView.ByCount });
        grouped.IsSuccess.ShouldBeTrue();
        grouped.Value.TotalGroupCount.ShouldBe(2);
        grouped.Value.TotalOperationCount.ShouldBe(3);
        grouped.Value.Groups.ShouldAllBe(group => group.Dimensions.Count == 1);
        grouped.Value.Groups.Single(group => group.Count == 2).Distribution.P50Milliseconds.ShouldBe(20);
        grouped.Value.Groups.Single(group => group.Count == 2).Distribution.P95Milliseconds.ShouldBe(30);
        var limited = await sut.GroupAsync(h.Query() with { GroupingDimensions = ["batch"], MaximumAnalysisCount = 1 });
        limited.IsSuccess.ShouldBeTrue();
        limited.Value.TotalOperationCount.ShouldBe(3);
        limited.Value.StatisticsLimitation.ShouldNotBeNull();
        limited.Value.Groups.ShouldAllBe(group => group.Distribution == null);
    }

    /// <summary>Grouping requires every selected root dimension before paging, counting and analysis, without consuming general predicate capacity.</summary>
    /// <example><code>await suite.GroupingDimensions_RequireEveryRootDimension(OperationProfilingView.Recent);</code></example>
    [Theory]
    [InlineData(OperationProfilingView.Recent)]
    [InlineData(OperationProfilingView.Slow)]
    [InlineData(OperationProfilingView.ByCount)]
    public async Task GroupingDimensions_RequireEveryRootDimension(OperationProfilingView view)
    {
        var h = await this.CreateAsync();
        var common = Enumerable.Range(0, 8).Select(index => Dimension($"filter{index}", index)).ToArray();
        var regionOnly = h.Record() with { Dimensions = [.. common, Dimension("region", "west")] };
        var channelOnly = h.Record() with { Dimensions = [.. common, Dimension("channel", "web")] };
        var empty = h.Record(durationMs: 30) with { Dimensions = [.. common, Dimension("region", ""), Dimension("channel", "web")] };
        var numeric = h.Record(durationMs: 50) with { Dimensions = [.. common, Dimension("REGION", 20), Dimension("CHANNEL", "web")] };
        var segmentOnly = h.Record() with { Dimensions = common };
        segmentOnly = segmentOnly with { Segments = [Summary(segmentOnly, ["Load"]) with { Dimensions = [SegmentDimension("region", "west"), SegmentDimension("channel", "web")] }] };
        var records = new[] { regionOnly, channelOnly, empty, numeric, segmentOnly };
        (await h.Store.AppendAsync(records.Select((record, index) => h.Envelope(record, index + 1)).ToArray())).IsSuccess.ShouldBeTrue();
        var query = h.Query() with
        {
            Dimensions = common.Select(dimension => new ProfilingDimensionPredicate { Key = dimension.Key, Value = dimension.Value }).ToArray(),
            GroupingDimensions = ["REGION", "channel"], PageSize = 1, View = view,
        };

        var first = await h.Store.QueryAsync(query);
        first.IsSuccess.ShouldBeTrue();
        first.Value.TotalCount.ShouldBe(2);
        first.Value.Records.Count.ShouldBe(1);
        first.Value.NextCursor.ShouldNotBeNull();
        var second = await h.Store.QueryAsync(query with { Cursor = first.Value.NextCursor });
        second.IsSuccess.ShouldBeTrue();
        second.Value.TotalCount.ShouldBe(2);
        new[] { first.Value.Records.Single().Id, second.Value.Records.Single().Id }.Order().ShouldBe(new[] { empty.Id, numeric.Id }.Order());
        second.Value.NextCursor.ShouldBeNull();
        var grouped = await h.Store.GroupAsync(query);
        grouped.IsSuccess.ShouldBeTrue();
        grouped.Value.TotalOperationCount.ShouldBe(2);
        grouped.Value.TotalGroupCount.ShouldBe(2);
        grouped.Value.Groups.ShouldAllBe(group => group.Dimensions.Count == 2);
        var analysis = await h.Store.SelectAnalysisAsync(query with { Boundary = first.Value.Boundary });
        analysis.IsSuccess.ShouldBeTrue();
        analysis.Value.Records.Select(record => record.Id).Order().ShouldBe(new[] { empty.Id, numeric.Id }.Order());

        (await h.Store.QueryAsync(query with { GroupingDimensions = [] })).Value.TotalCount.ShouldBe(5);
        var missing = await h.Store.QueryAsync(query with { GroupingDimensions = [], GroupDimensions = [new() { Key = "region", Operator = ProfilingDimensionOperator.Missing }] });
        missing.IsSuccess.ShouldBeTrue();
        missing.Value.TotalCount.ShouldBe(2);
        var unknown = query with { GroupingDimensions = ["unknown"] };
        (await h.Store.QueryAsync(unknown)).Value.TotalCount.ShouldBe(0);
        var noGroups = await h.Store.GroupAsync(unknown);
        noGroups.IsSuccess.ShouldBeTrue();
        noGroups.Value.TotalOperationCount.ShouldBe(0);
        noGroups.Value.TotalGroupCount.ShouldBe(0);
        noGroups.Value.Groups.ShouldBeEmpty();
    }

    /// <summary>Preserves all general filters while expanding a grouped bucket, including missing typed values.</summary>
    [Fact]
    public async Task GroupExpansion_WithEightPredicatesAndMissingDimension_IsBoundedAndExact()
    {
        var h = await this.CreateAsync();
        var common = Enumerable.Range(0, 8).Select(index => Dimension($"filter{index}", index)).ToArray();
        var missing = h.Record() with { Dimensions = common };
        var present = h.Record() with { Dimensions = common.Append(Dimension("region", "west")).ToArray() };
        (await h.Store.AppendAsync([h.Envelope(missing, 1), h.Envelope(present, 2)])).IsSuccess.ShouldBeTrue();
        var query = h.Query() with
        {
            Dimensions = common.Select(value => new ProfilingDimensionPredicate { Key = value.Key, Value = value.Value }).ToArray(),
            GroupDimensions = [new() { Key = "REGION", Operator = ProfilingDimensionOperator.Missing }],
        };

        var result = await h.Store.QueryAsync(query);
        result.IsSuccess.ShouldBeTrue();
        result.Value.Records.Single().Id.ShouldBe(missing.Id);
        var groups = await h.Store.GroupAsync(query);
        groups.Value.TotalOperationCount.ShouldBe(1);
        (await h.Store.QueryAsync(query with { GroupDimensions = Enumerable.Range(0, 5).Select(index => new ProfilingDimensionPredicate { Key = $"g{index}", Operator = ProfilingDimensionOperator.Missing }).ToArray() })).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task Append_Retry_ReturnsSameCommitAndImmutableWholeGraph()
    {
        // Arrange
        var h = await this.CreateAsync();
        var dimensions = new List<ProfilingDimension> { Dimension("tenant", "sample") };
        var record = h.Record() with { Dimensions = dimensions };
        var envelope = h.Envelope(record, 1);

        // Act
        var first = await h.Store.AppendAsync([envelope]);
        dimensions.Clear();
        var retry = await h.Store.AppendAsync([envelope]);
        var retained = await h.Store.FindAsync(record.Id);

        // Assert
        first.IsSuccess.ShouldBeTrue();
        first.Value.Records.Single().Outcome.ShouldBe(ProfilingWriteOutcome.Accepted);
        retry.Value.Records.Single().Outcome.ShouldBe(ProfilingWriteOutcome.AlreadyStored);
        retry.Value.Records.Single().CommitWatermark.ShouldBe(first.Value.Records.Single().CommitWatermark);
        retained.Value.Dimensions.Single().Value.Scalar.ShouldBe("sample");
        ((ICollection<ProfilingDimension>)retained.Value.Dimensions).IsReadOnly.ShouldBeTrue();
    }

    [Fact]
    public async Task Append_InvalidMember_DoesNotLoseOtherBatchMembers()
    {
        // Arrange
        var h = await this.CreateAsync();
        var first = h.Record();
        var last = h.Record();
        var invalid = h.Record() with { Key = new string('x', 129) };

        // Act
        var result = await h.Store.AppendAsync([h.Envelope(first, 1), h.Envelope(invalid, 2), h.Envelope(last, 3)]);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Records.Select(r => r.Outcome).ShouldBe([ProfilingWriteOutcome.Accepted, ProfilingWriteOutcome.PermanentFailure, ProfilingWriteOutcome.Accepted]);
        (await h.Store.FindAsync(invalid.Id)).Value.ShouldBeNull();
        (await h.Store.FindAsync(first.Id)).Value.ShouldNotBeNull();
        (await h.Store.FindAsync(last.Id)).Value.ShouldNotBeNull();
    }

    [Fact]
    public async Task Append_IdentityAndSequenceConflicts_AreNotSilentRetries()
    {
        // Arrange
        var h = await this.CreateAsync();
        var record = h.Record();
        await h.Store.AppendAsync([h.Envelope(record, 1)]);

        // Act
        var sameId = await h.Store.AppendAsync([h.Envelope(record, 2)]);
        var sameSequence = await h.Store.AppendAsync([h.Envelope(h.Record(), 1)]);

        // Assert
        sameId.Value.Records.Single().SafeCode.ShouldBe("OperationIdentityConflict");
        sameSequence.Value.Records.Single().SafeCode.ShouldBe("SequenceIdentityConflict");
    }

    [Fact]
    public async Task Retention_ProtectsUnknownCommit_ThenSettlementPreventsResurrection()
    {
        // Arrange
        var options = new ProfilingOptions();
        options.Operations.MaximumRetainedOperations = 1;
        var h = await this.CreateAsync(options);
        var first = h.Envelope(h.Record(), 1);
        var second = h.Envelope(h.Record(), 2);
        await h.Store.AppendAsync([first]);

        // Act
        var blocked = await h.Store.AppendAsync([second]);
        await h.SettleAsync(1);
        var admitted = await h.Store.AppendAsync([second]);
        var retry = await h.Store.AppendAsync([first]);

        // Assert
        blocked.Value.Records.Single().Outcome.ShouldBe(ProfilingWriteOutcome.CapacityRejected);
        blocked.Value.Records.Single().SafeCode.ShouldBe("RetentionCapacity");
        admitted.Value.Records.Single().Outcome.ShouldBe(ProfilingWriteOutcome.Accepted);
        (await h.Store.FindAsync(first.Record.Id)).Value.ShouldBeNull();
        retry.Value.Records.Single().Outcome.ShouldBe(ProfilingWriteOutcome.AlreadySettled);
        (await h.Store.FindAsync(first.Record.Id)).Value.ShouldBeNull();
    }

    [Fact]
    public async Task Query_PublicationBoundary_ExcludesLaterBackdatedCommit()
    {
        // Arrange
        var h = await this.CreateAsync();
        var first = h.Record(durationMs: 100);
        var second = h.Record(durationMs: 50);
        await h.Store.AppendAsync([h.Envelope(first, 1), h.Envelope(second, 2)]);
        var query = h.Query() with { PageSize = 1 };
        var page = await h.Store.QueryAsync(query);
        await h.Store.AppendAsync([h.Envelope(h.Record(durationMs: 75), 3)]);

        // Act
        var next = await h.Store.QueryAsync(query with { Cursor = page.Value.NextCursor });

        // Assert
        page.Value.TotalCount.ShouldBe(2);
        page.Value.Records.Single().Id.ShouldBe(first.Id);
        next.IsSuccess.ShouldBeTrue();
        next.Value.TotalCount.ShouldBe(2);
        next.Value.Records.Single().Id.ShouldBe(second.Id);
        next.Value.HasMore.ShouldBeFalse();
    }

    [Fact]
    public async Task Query_DeletionRevision_InvalidatesBoundaryAndRetainedCursor()
    {
        // Arrange
        var h = await this.CreateAsync();
        await h.Store.AppendAsync([h.Envelope(h.Record(), 1), h.Envelope(h.Record(), 2)]);
        var query = h.Query() with { PageSize = 1 };
        var page = await h.Store.QueryAsync(query);
        await h.SettleAsync(2);
        await h.Provider.ResumeMaintenanceAsync(new ProfilingMaintenanceRequest { MaximumOperationCount = 1 });

        // Act
        var next = await h.Store.QueryAsync(query with { Cursor = page.Value.NextCursor });

        // Assert
        next.IsFailure.ShouldBeTrue();
        next.Errors.ShouldContain(e => e is ProfilingQueryBoundaryError);
    }

    [Fact]
    public async Task Queries_TypedCanonicalNames_KeepScalarTypesAndValuesDistinct()
    {
        // Arrange
        var h = await this.CreateAsync();
        var values = new object[] { long.MaxValue, 42m, 42d, "Load", "load", "", " " };
        for (var index = 0; index < values.Length; index++)
        {
            await h.Store.AppendAsync([h.Envelope(h.Record(key: index % 2 == 0 ? "Load" : "load") with { Dimensions = [Dimension("TYPE", values[index])] }, index + 1)]);
        }

        // Act/Assert
        foreach (var value in values)
        {
            ProfilingValue.TryCreate(value, out var scalar).ShouldBeTrue();
            var selection = await h.Store.QueryAsync(h.Query() with { Key = "LOAD", Dimensions = [new() { Key = "type", Value = scalar }] });
            selection.Value.TotalCount.ShouldBe(1);
            selection.Value.Records.Single().Dimensions.Single().Value.ShouldBe(scalar);
        }
    }

    [Fact]
    public async Task ExactLookup_IgnoresDefaultListOutcomeAndWindow()
    {
        // Arrange
        var h = await this.CreateAsync();
        var record = h.Record() with { Outcome = OperationProfilingOutcome.Failed, CompletedUtc = h.Clock.GetUtcNow().AddHours(-2) };
        await h.Store.AppendAsync([h.Envelope(record, 1)]);

        // Act
        var list = await h.Store.QueryAsync(h.Query());
        var exact = await h.Store.FindAsync(record.Id);

        // Assert
        list.Value.TotalCount.ShouldBe(0);
        exact.Value.Id.ShouldBe(record.Id);
        exact.Value.Outcome.ShouldBe(OperationProfilingOutcome.Failed);
    }

    [Fact]
    public async Task Analysis_SelectionLimit_IsExplicitAndNeverFirstPageTruncation()
    {
        // Arrange
        var h = await this.CreateAsync();
        await h.Store.AppendAsync([h.Envelope(h.Record(), 1), h.Envelope(h.Record(), 2)]);

        // Act
        var rejected = await h.Store.SelectAnalysisAsync(h.Query() with { MaximumAnalysisCount = 1 });
        var accepted = await h.Store.SelectAnalysisAsync(h.Query() with { PageSize = 1, MaximumAnalysisCount = 2 });

        // Assert
        rejected.IsFailure.ShouldBeTrue();
        rejected.Errors.ShouldContain(e => e is ProfilingQueryLimitError);
        accepted.Value.Records.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GroupViews_LimitOccurrencesOrGroups_WhileCountsCoverFullBoundary()
    {
        // Arrange
        var h = await this.CreateAsync();
        await h.Store.AppendAsync([h.Envelope(h.Record("Report", 100), 1), h.Envelope(h.Record("report", 90), 2), h.Envelope(h.Record("Report", 80), 3), h.Envelope(h.Record("Other", 50), 4)]);

        // Act
        var slow = await h.Store.GroupAsync(h.Query() with { PageSize = 2 });
        var count = await h.Store.GroupAsync(h.Query() with { View = OperationProfilingView.ByCount, PageSize = 1 });

        // Assert
        slow.Value.TotalOperationCount.ShouldBe(4);
        slow.Value.TotalGroupCount.ShouldBe(2);
        slow.Value.Groups.Single().Count.ShouldBe(3);
        slow.Value.Groups.Single().Occurrences.Count.ShouldBe(2);
        slow.Value.HasMore.ShouldBeTrue();
        count.Value.Groups.Single().Count.ShouldBe(3);
        count.Value.Groups.Single().Occurrences.ShouldBeEmpty();
        count.Value.HasMore.ShouldBeTrue();
    }

    protected async Task<Harness> CreateAsync(ProfilingOptions options = null)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 8, 10, 0, 0, TimeSpan.Zero));
        var provider = await this.CreateProviderAsync(options ?? new ProfilingOptions(), clock);
        var node = new ProfilingNodeIdentityProvider(clock).GetNode();
        var lease = await provider.Operations.OpenWriterAsync(new() { AttemptId = Guid.NewGuid(), Node = node });
        lease.IsSuccess.ShouldBeTrue();
        return new(provider, clock, node, lease.Value);
    }

    protected static ProfilingDimension Dimension(string key, object value)
    {
        ProfilingValue.TryCreate(value, out var scalar).ShouldBeTrue();
        return new() { Key = key, Value = scalar };
    }

    protected sealed record Harness(IProfilingStorageProvider Provider, FakeTimeProvider Clock, ProfilingNode Node, ProfilingWriterLease Lease)
    {
        public IOperationProfilingStore Store => this.Provider.Operations;
        public OperationProfilingRecord Record(string key = "Report", int durationMs = 10) => new()
        {
            Id = Guid.NewGuid(), Key = key, Kind = "Service", Node = this.Node,
            StartedUtc = this.Clock.GetUtcNow().AddMilliseconds(-durationMs), CompletedUtc = this.Clock.GetUtcNow(), Duration = TimeSpan.FromMilliseconds(durationMs),
            Outcome = OperationProfilingOutcome.Completed, TimestampFrequency = this.Clock.TimestampFrequency,
            WallTime = [new() { Kind = ProfilingWallTimeKind.OutsideSegments, Duration = TimeSpan.FromMilliseconds(durationMs) }],
        };
        public ProfilingWriteEnvelope Envelope(OperationProfilingRecord record, long sequence, ProfilingWriterLease lease = null) => new() { Record = record, CompletionSequence = sequence, Lease = lease ?? this.Lease };
        public OperationProfilingQuery Query() => new() { FromUtc = this.Clock.GetUtcNow().AddMinutes(-15), ToUtc = this.Clock.GetUtcNow().AddMinutes(1) };
        public Task<IResult<ProfilingWriterSynchronizationResult>> SettleAsync(long sequence) => this.Store.SynchronizeWriterAsync(new() { Lease = this.Lease, SettledThrough = sequence });
    }
}
