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
