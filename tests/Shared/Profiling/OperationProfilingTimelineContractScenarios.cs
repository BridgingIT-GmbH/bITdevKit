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
    /// <summary>The timeline counts every retained start, including all outcomes and beyond page/analysis limits, with canonical kind lanes.</summary>
    /// <example><code>await suite.Timeline_AllProviders_CountsStartsWithoutAnalysisLimits();</code></example>
    [Fact]
    public async Task Timeline_AllProviders_CountsStartsWithoutAnalysisLimits()
    {
        var options = new ProfilingOptions { Enabled = true };
        options.Queries.MaximumAnalysisRecords = 1;
        var h = await this.CreateAsync(options);
        var from = h.Clock.GetUtcNow().AddMinutes(-10);
        var records = new[]
        {
            h.Record("store:write") with { StartedUtc = from, CompletedUtc = from.AddSeconds(1), Kind = "FileStorage" },
            h.Record("store:write") with { StartedUtc = from.AddSeconds(1), CompletedUtc = from.AddSeconds(2), Kind = "FILESTORAGE", Outcome = OperationProfilingOutcome.Failed },
            h.Record("store:read") with { StartedUtc = from.AddSeconds(1), CompletedUtc = from.AddSeconds(2), Kind = "BlobStorage", Outcome = OperationProfilingOutcome.Canceled },
            h.Record("other") with { StartedUtc = from.AddMinutes(-1), CompletedUtc = from.AddMinutes(1) },
            h.Record("long") with { StartedUtc = from.AddMinutes(9), CompletedUtc = h.Clock.GetUtcNow().AddMinutes(1) },
            h.Record("end") with { StartedUtc = h.Clock.GetUtcNow(), CompletedUtc = h.Clock.GetUtcNow() },
        };
        (await h.Store.AppendAsync(records.Select((record, index) => h.Envelope(record, index + 1)).ToArray())).IsSuccess.ShouldBeTrue();
        var query = h.Query() with { FromUtc = from, ToUtc = h.Clock.GetUtcNow(), StartedWithin = true, Outcomes = [], PageSize = 1, MaximumAnalysisCount = 1 };
        var sut = new OperationProfilingQueryService(h.Provider, options, null, clock: h.Clock);

        var result = await sut.TimelineAsync(query, 12);

        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalCount.ShouldBe(4);
        result.Value.Buckets.Sum(bucket => bucket.Count).ShouldBe(4);
        result.Value.Buckets.Single(bucket => ProfilingKeyComparer.Instance.Equals(bucket.Kind, "FileStorage")).Count.ShouldBe(2);
        result.Value.Buckets.Sum(bucket => bucket.FailedCount).ShouldBe(1);
        result.Value.Buckets.Sum(bucket => bucket.CanceledCount).ShouldBe(1);
        result.Value.Buckets.ShouldAllBe(bucket => bucket.FromUtc >= from && bucket.ToUtc <= query.ToUtc);
        var selected = await sut.TimelineAsync(query with { Kind = "FILESTORAGE", KeyContains = "WRITE", NodeId = h.Node.Identity.Id }, 12);
        selected.IsSuccess.ShouldBeTrue();
        selected.Value.TotalCount.ShouldBe(2);
        var bucket = selected.Value.Buckets.Single();
        var page = await sut.QueryAsync(query with { FromUtc = bucket.FromUtc, ToUtc = bucket.ToUtc, Kind = bucket.Kind, PageSize = 100 });
        page.IsSuccess.ShouldBeTrue();
        page.Value.TotalCount.ShouldBe(2);
        page.Value.Records.Select(record => record.Id).Order().ShouldBe(records.Take(2).Select(record => record.Id).Order());
        var missingNode = await sut.TimelineAsync(query with { NodeId = Guid.NewGuid() }, 12);
        missingNode.Value.TotalCount.ShouldBe(0);
        missingNode.Value.Nodes.Single().Identity.Id.ShouldBe(h.Node.Identity.Id);
    }

    /// <summary>Invalid bucket counts, conflicting time selectors and unsupported cursors fail before reading history.</summary>
    /// <example><code>await suite.Timeline_InvalidSelection_IsRejected();</code></example>
    [Fact]
    public async Task Timeline_InvalidSelection_IsRejected()
    {
        var h = await this.CreateAsync();
        var query = h.Query() with { StartedWithin = true };
        (await h.Store.TimelineAsync(query, 11)).IsFailure.ShouldBeTrue();
        (await h.Store.TimelineAsync(query, 257)).IsFailure.ShouldBeTrue();
        (await h.Store.TimelineAsync(query with { IntervalOverlap = true })).IsFailure.ShouldBeTrue();
        (await h.Store.TimelineAsync(query with { StartedWithin = false })).IsFailure.ShouldBeTrue();
        (await h.Store.TimelineAsync(query with { Cursor = "invalid" })).IsFailure.ShouldBeTrue();
    }

    /// <summary>Too many kind lanes fail visibly rather than silently omitting recorded operations.</summary>
    /// <example><code>await suite.Timeline_KindLimit_RequiresNarrowingTheSelection();</code></example>
    [Fact]
    public async Task Timeline_KindLimit_RequiresNarrowingTheSelection()
    {
        var h = await this.CreateAsync();
        var records = Enumerable.Range(0, OperationProfilingTimeline.MaximumKinds + 1)
            .Select(index => h.Record() with { Kind = "Kind" + index }).ToArray();
        (await h.Store.AppendAsync(records.Select((record, index) => h.Envelope(record, index + 1)).ToArray())).IsSuccess.ShouldBeTrue();
        var query = h.Query() with { StartedWithin = true, Outcomes = [] };
        var result = await h.Store.TimelineAsync(query, 12);
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(error => error is ProfilingQueryLimitError);
        var narrowed = await h.Store.TimelineAsync(query with { Kind = "Kind0" }, 12);
        narrowed.IsSuccess.ShouldBeTrue();
        narrowed.Value.TotalCount.ShouldBe(1);
    }
}
