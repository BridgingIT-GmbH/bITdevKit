// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Tests.Profiling;

using BridgingIT.DevKit.Common;
using Shouldly;
using Xunit;

public abstract partial class ProfilingStorageContractTestsBase
{
    /// <summary>Node choices cover retained matches beyond the visible page and remain selectable when one node is filtered.</summary>
    /// <example><code>await suite.Groups_NodeFilter_ReturnsAllContributingNodesWithinPublicationBoundary(OperationProfilingView.Slow);</code></example>
    [Theory]
    [InlineData(OperationProfilingView.Slow)]
    [InlineData(OperationProfilingView.Recent)]
    [InlineData(OperationProfilingView.ByCount)]
    public async Task Groups_NodeFilter_ReturnsAllContributingNodesWithinPublicationBoundary(OperationProfilingView view)
    {
        var h = await this.CreateAsync();
        var otherNode = h.Node with { Identity = new(Guid.NewGuid(), "zzzzzzzz"), HostName = "other-node", Correlation = null };
        var otherLease = await h.Store.OpenWriterAsync(new() { AttemptId = Guid.NewGuid(), Node = otherNode });
        otherLease.IsSuccess.ShouldBeTrue();
        (await h.Store.AppendAsync([h.Envelope(h.Record(), 1), h.Envelope(h.Record() with { Node = otherNode }, 1, otherLease.Value)])).IsSuccess.ShouldBeTrue();
        var query = h.Query() with { View = view, PageSize = 1, NodeId = h.Node.Identity.Id };

        var page = await h.Store.GroupAsync(query);

        page.IsSuccess.ShouldBeTrue();
        page.Value.TotalOperationCount.ShouldBe(1);
        page.Value.Nodes.Select(node => node.Identity.Id).Order().ShouldBe(new[] { h.Node.Identity.Id, otherNode.Identity.Id }.Order());
        page.Value.NodesTruncated.ShouldBeFalse();
        (await h.Store.AppendAsync([h.Envelope(h.Record("Other") with { Node = otherNode }, 2, otherLease.Value)])).IsSuccess.ShouldBeTrue();
        var absent = await h.Store.GroupAsync(query with { Key = "Other", NodeId = null });
        absent.Value.Nodes.Select(node => node.Identity.Id).ShouldBe([otherNode.Identity.Id]);
        var lateNode = h.Node with { Identity = new(Guid.NewGuid(), "xxxxxxxx"), HostName = "late-node", Correlation = null };
        var lateLease = await h.Store.OpenWriterAsync(new() { AttemptId = Guid.NewGuid(), Node = lateNode });
        (await h.Store.AppendAsync([h.Envelope(h.Record() with { Node = lateNode }, 1, lateLease.Value)])).IsSuccess.ShouldBeTrue();
        (await h.Store.GroupAsync(query)).Value.Nodes.Count.ShouldBe(3);
        var pinned = await h.Store.GroupAsync(query with { Boundary = page.Value.Boundary });
        pinned.Value.TotalOperationCount.ShouldBe(1);
        pinned.Value.Nodes.Count.ShouldBe(2);
    }

    /// <summary>Node choice limits do not truncate execution counts or the selected node's results.</summary>
    /// <example><code>await suite.Groups_NodeChoiceLimit_ReportsTruncationWithoutRestrictingResults();</code></example>
    [Fact]
    public async Task Groups_NodeChoiceLimit_ReportsTruncationWithoutRestrictingResults()
    {
        var options = new ProfilingOptions();
        options.Queries.MaximumNodeChoices = 1;
        var h = await this.CreateAsync(options);
        var otherNode = h.Node with { Identity = new(Guid.NewGuid(), "yyyyyyyy"), HostName = "other-node", Correlation = null };
        var lease = await h.Store.OpenWriterAsync(new() { AttemptId = Guid.NewGuid(), Node = otherNode });
        (await h.Store.AppendAsync([h.Envelope(h.Record(), 1), h.Envelope(h.Record() with { Node = otherNode }, 1, lease.Value)])).IsSuccess.ShouldBeTrue();

        var page = await h.Store.GroupAsync(h.Query());

        page.IsSuccess.ShouldBeTrue();
        page.Value.TotalOperationCount.ShouldBe(2);
        page.Value.Nodes.Count.ShouldBe(1);
        page.Value.NodesTruncated.ShouldBeTrue();
    }

    [Theory]
    [InlineData("key")]
    [InlineData("sort")]
    [InlineData("expiry")]
    [InlineData("cursor")]
    [InlineData("watermark")]
    public async Task Cursor_ChangedSelectionOrBoundary_ReturnsExplicitRefreshFailure(string change)
    {
        // Arrange
        var h = await this.CreateAsync();
        await h.Store.AppendAsync([h.Envelope(h.Record(), 1), h.Envelope(h.Record(), 2)]);
        var query = h.Query() with { PageSize = 1 };
        var page = await h.Store.QueryAsync(query);
        var next = query with { Cursor = page.Value.NextCursor };
        switch (change)
        {
            case "key": next = next with { Key = "other" }; break;
            case "sort": next = next with { View = OperationProfilingView.Recent }; break;
            case "expiry": h.Clock.Advance(TimeSpan.FromMinutes(5)); break;
            case "cursor": next = next with { Cursor = next.Cursor[..^1] + (next.Cursor[^1] == 'a' ? "b" : "a") }; break;
            case "watermark": next = query with { Boundary = page.Value.Boundary with { CommitWatermark = 0 } }; break;
        }

        // Act
        var result = await h.Store.QueryAsync(next);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(error => error is ProfilingQueryBoundaryError);
    }

    [Fact]
    public async Task Cursor_EvaluatedDefaultWindow_StaysFixedWhileClockMoves()
    {
        // Arrange
        var h = await this.CreateAsync();
        await h.Store.AppendAsync([h.Envelope(h.Record() with { CompletedUtc = h.Clock.GetUtcNow().AddSeconds(-1) }, 1), h.Envelope(h.Record() with { CompletedUtc = h.Clock.GetUtcNow().AddSeconds(-2) }, 2)]);
        var query = new OperationProfilingQuery { PageSize = 1 };
        var page = await h.Store.QueryAsync(query);
        h.Clock.Advance(TimeSpan.FromMinutes(1));

        // Act
        var next = await h.Store.QueryAsync(query with { Cursor = page.Value.NextCursor });

        // Assert
        next.IsSuccess.ShouldBeTrue();
        next.Value.Boundary.FromUtc.ShouldBe(page.Value.Boundary.FromUtc);
        next.Value.Boundary.ToUtc.ShouldBe(page.Value.Boundary.ToUtc);
        next.Value.TotalCount.ShouldBe(2);
        next.Value.Records.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(OperationProfilingView.Slow)]
    [InlineData(OperationProfilingView.Recent)]
    public async Task Query_TiedOrder_UsesCanonicalGuidOrderAcrossPages(OperationProfilingView view)
    {
        // Arrange
        var h = await this.CreateAsync();
        var ids = new[] { Guid.Parse("ffffffff-0000-0000-0000-000000000000"), Guid.Parse("00000001-ffff-ffff-ffff-ffffffffffff"), Guid.Parse("00000000-0000-0000-0000-000000000001") };
        for (var index = 0; index < ids.Length; index++)
        {
            await h.Store.AppendAsync([h.Envelope(h.Record() with { Id = ids[index] }, index + 1)]);
        }

        var query = h.Query() with { View = view, PageSize = 1 };
        var actual = new List<Guid>();

        // Act
        while (true)
        {
            var page = await h.Store.QueryAsync(query);
            actual.AddRange(page.Value.Records.Select(record => record.Id));
            if (!page.Value.HasMore) { break; }

            query = query with { Cursor = page.Value.NextCursor };
        }

        // Assert
        var expected = ids.OrderBy(id => id.ToString("N"), StringComparer.Ordinal).ToArray();
        if (view == OperationProfilingView.Recent) { Array.Reverse(expected); }

        actual.ShouldBe(expected);
    }

    [Fact]
    public async Task SegmentPredicates_AllMatchSameSummary_AndKeepParentPathsDistinct()
    {
        // Arrange
        var h = await this.CreateAsync();
        var record = h.Record();
        var parentA = Summary(record, ["Load"]);
        var parentB = Summary(record, ["Save"]);
        var load = Summary(record, ["Load", "Read"]) with { Dimensions = [SegmentDimension("source", "sql")] };
        var save = Summary(record, ["Save", "Read"]) with { Dimensions = [SegmentDimension("source", "cache")] };
        record = record with { Segments = [parentA, parentB, load, save] };
        await h.Store.AppendAsync([h.Envelope(record, 1)]);
        ProfilingValue.TryCreate("cache", out var cache);
        ProfilingValue.TryCreate("sql", out var sql);
        var query = h.Query() with { SegmentPath = new ProfilingSegmentPath(["load", "read"]) };

        // Act
        var crossed = await h.Store.QueryAsync(query with { SegmentDimensions = [new() { Key = "source", Value = cache }] });
        var matching = await h.Store.QueryAsync(query with { SegmentDimensions = [new() { Key = "SOURCE", Value = sql }] });

        // Assert
        crossed.Value.TotalCount.ShouldBe(0);
        matching.Value.TotalCount.ShouldBe(1);
        matching.Value.Records.Single().Segments.Count.ShouldBe(4);
    }

    [Fact]
    public async Task Dimensions_PresentMissingAndMixed_DoNotCollapseMissingIntoEmptyValue()
    {
        // Arrange
        var h = await this.CreateAsync();
        var absent = h.Record();
        var empty = h.Record() with { Dimensions = [Dimension("label", "")] };
        var mixed = h.Record();
        mixed = mixed with { Segments = [Summary(mixed, ["Read"]) with { Dimensions = [new() { Key = "label", Mixed = true, SampleCount = 2 }] }] };
        await h.Store.AppendAsync([h.Envelope(absent, 1), h.Envelope(empty, 2), h.Envelope(mixed, 3)]);

        // Act
        var present = await h.Store.QueryAsync(h.Query() with { Dimensions = [new() { Key = "label", Operator = ProfilingDimensionOperator.Present }] });
        var missing = await h.Store.QueryAsync(h.Query() with { Dimensions = [new() { Key = "label", Operator = ProfilingDimensionOperator.Missing }] });
        var mixedSelection = await h.Store.QueryAsync(h.Query() with { SegmentDimensions = [new() { Key = "label", Operator = ProfilingDimensionOperator.Mixed }] });

        // Assert
        present.Value.TotalCount.ShouldBe(1);
        present.Value.Records.Single().Id.ShouldBe(empty.Id);
        missing.Value.TotalCount.ShouldBe(2);
        mixedSelection.Value.Records.Single().Id.ShouldBe(mixed.Id);
    }

    [Fact]
    public async Task GroupingDimensions_DistinguishTypesAndMissingValues()
    {
        // Arrange
        var h = await this.CreateAsync();
        var values = new object[] { 1L, 1m, 1d, "1", "", null };
        for (var index = 0; index < values.Length; index++)
        {
            var record = h.Record() with { Dimensions = values[index] is null ? [] : [Dimension("workload", values[index])] };
            await h.Store.AppendAsync([h.Envelope(record, index + 1)]);
        }

        // Act
        var result = await h.Store.GroupAsync(h.Query() with { GroupingDimensions = ["WORKLOAD"], View = OperationProfilingView.ByCount });

        // Assert
        result.Value.TotalGroupCount.ShouldBe(6);
        result.Value.Groups.Select(group => group.ComparisonKey).Distinct(StringComparer.Ordinal).Count().ShouldBe(6);
        result.Value.Groups.All(group => group.Count == 1).ShouldBeTrue();
    }

    [Fact]
    public async Task HttpFilters_UseAdapterProjection_WithoutExcludingNonHttpExactLookup()
    {
        // Arrange
        var h = await this.CreateAsync();
        var http = h.Record() with { Kind = "HttpRequest", Http = new() { Method = "GET", Path = "/reports/1", Route = "/reports/{id}", StatusCode = 201, ApplicationRequestId = "request-1", SamplingStrategyKey = "All", SamplingConfigurationKey = "default" } };
        var service = h.Record();
        await h.Store.AppendAsync([h.Envelope(http, 1), h.Envelope(service, 2)]);

        // Act
        var selected = await h.Store.QueryAsync(h.Query() with { Kind = "httprequest", HttpMethod = "get", Route = "/REPORTS/{ID}", HttpStatusCode = 201, ApplicationRequestId = "request-1", SamplingStrategyKey = "all" });
        var absent = await h.Store.QueryAsync(h.Query() with { HttpStatusCode = 200 });

        // Assert
        selected.Value.Records.Single().Id.ShouldBe(http.Id);
        absent.Value.TotalCount.ShouldBe(0);
        (await h.Store.FindAsync(service.Id)).Value.ShouldNotBeNull();
    }

    [Fact]
    public async Task Append_InvalidSummaryParentOrHttpMetadata_NeverStoresPartialGraph()
    {
        // Arrange
        var h = await this.CreateAsync();
        var noParent = h.Record();
        noParent = noParent with { Segments = [Summary(noParent, ["Load", "Read"])] };
        var http = h.Record() with { Http = new() { ResponseBytes = -1 } };
        var duplicate = h.Record() with { Dimensions = [Dimension("Key", 1), Dimension("key", 2)] };

        // Act
        var result = await h.Store.AppendAsync([h.Envelope(noParent, 1), h.Envelope(http, 2), h.Envelope(duplicate, 3)]);

        // Assert
        result.Value.Records.All(record => record.Outcome == ProfilingWriteOutcome.PermanentFailure).ShouldBeTrue();
        (await h.Store.QueryAsync(h.Query())).Value.TotalCount.ShouldBe(0);
    }

    protected static ProfilingSegmentSummary Summary(OperationProfilingRecord root, string[] path) => new()
    {
        OperationId = root.Id, NodeId = root.NodeId, Key = path[^1], Path = new(path), ParentPath = path.Length > 1 ? new(path[..^1]) : null,
        Statistics = new() { Count = 1, TotalDuration = TimeSpan.FromMilliseconds(1), TotalSelfDuration = TimeSpan.FromMilliseconds(1), MinimumDuration = TimeSpan.FromMilliseconds(1), MaximumDuration = TimeSpan.FromMilliseconds(1) },
        Outcomes = [new() { Outcome = ProfilingSegmentOutcome.Completed, Statistics = new() { Count = 1, TotalDuration = TimeSpan.FromMilliseconds(1), TotalSelfDuration = TimeSpan.FromMilliseconds(1), MinimumDuration = TimeSpan.FromMilliseconds(1), MaximumDuration = TimeSpan.FromMilliseconds(1) } }],
    };

    protected static ProfilingSegmentDimensionSummary SegmentDimension(string key, object value) => new() { Key = key, Value = Dimension(key, value).Value, SampleCount = 1 };
}
