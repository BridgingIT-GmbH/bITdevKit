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
    /// <summary>Selective deletion removes the complete filtered group and graph, preserves other groups and later work, and remains resumable under batching.</summary>
    /// <example><code>await suite.Clear_OperationGroup_RespectsFiltersAndWriterCutoffs(true);</code></example>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Clear_OperationGroup_RespectsFiltersAndWriterCutoffs(bool bounded)
    {
        var options = new ProfilingOptions();
        if (bounded) { options.Storage.MaximumMaintenanceRoots = 1; }

        var h = await this.CreateAsync(options);
        (await h.Provider.Runtime.GetOrCreateActiveSessionAsync(RuntimeRequest(h))).IsSuccess.ShouldBeTrue();
        var targets = Enumerable.Range(0, 5).Select(index =>
        {
            var record = h.Record(index % 2 == 0 ? "catalog:load" : "CATALOG:LOAD") with { Dimensions = [Dimension("batch", 1)] };
            return record with { Segments = [Summary(record, ["Load"])] };
        }).ToArray();
        var wrongValue = h.Record("catalog:load") with { Dimensions = [Dimension("batch", "1")] };
        var wrongKind = h.Record("catalog:load") with { Kind = "HttpRequest", Dimensions = [Dimension("batch", 1)] };
        var wrongKey = h.Record("catalog:other") with { Dimensions = [Dimension("batch", 1)] };
        var outside = h.Record("catalog:load") with { Dimensions = [Dimension("batch", 1)], StartedUtc = h.Clock.GetUtcNow().AddMinutes(-16).AddMilliseconds(-10), CompletedUtc = h.Clock.GetUtcNow().AddMinutes(-16) };
        var kept = new[] { wrongValue, wrongKind, wrongKey, outside };
        var old = targets.Concat(kept).Select((record, index) => h.Envelope(record, index + 1)).ToArray();
        var appended = await h.Store.AppendAsync(old);
        appended.IsSuccess.ShouldBeTrue();
        appended.Value.Records.ShouldAllBe(record => record.Outcome == ProfilingWriteOutcome.Accepted);
        var otherNode = h.Node with { Identity = ProfilingIdentityFactory.CreateNode() };
        var otherLease = (await h.Store.OpenWriterAsync(new() { AttemptId = Guid.NewGuid(), Node = otherNode })).Value;
        var other = h.Envelope(h.Record("catalog:load") with { Node = otherNode, Dimensions = [Dimension("batch", 1)] }, 1, otherLease);
        (await h.Store.AppendAsync([other])).IsSuccess.ShouldBeTrue();
        var selection = h.Query() with
        {
            Key = "CATALOG:LOAD", Kind = "service", NodeId = h.Node.Identity.Id, PageSize = 1,
            GroupDimensions = [new() { Key = "BATCH", Value = Dimension("batch", 1).Value }],
        };
        var page = await h.Store.QueryAsync(selection);
        page.Value.TotalCount.ShouldBe(5);
        var clearing = h.Provider.ClearAsync(new() { DataSet = ProfilingDataSet.Operations, OperationSelection = selection });
        var pending = await PendingAsync(h, h.Lease);
        pending.Selection.OperationSelection.Key.ShouldBe(ProfilingKeyComparer.Canonicalize(selection.Key));
        var later = h.Envelope(h.Record("catalog:load") with { Dimensions = [Dimension("batch", 1)] }, old.Length + 1);
        (await h.Store.AppendAsync([later])).IsSuccess.ShouldBeTrue();
        await AckAsync(h, pending.Id, h.Lease, old.Length);
        await AckAsync(h, pending.Id, otherLease, 1);
        var result = await clearing.WaitAsync(TimeSpan.FromSeconds(10));
        result.IsSuccess.ShouldBeTrue();
        var removed = result.Value.RemovedOperationCount;
        var summaries = result.Value.RemovedSegmentSummaryCount;
        if (bounded)
        {
            result.Value.State.ShouldBe(ProfilingClearState.Applying);
            for (var index = 0; index < 10; index++)
            {
                var progress = await h.Provider.ResumeMaintenanceAsync(new() { MaximumRoots = 1 });
                progress.IsSuccess.ShouldBeTrue();
                removed += progress.Value.RemovedOperations;
                if (progress.Value.RemainingClears == 0) break;
            }
        }

        removed.ShouldBe(5);
        summaries.ShouldBe(bounded ? 1 : 5);
        foreach (var record in targets) { (await h.Store.FindAsync(record.Id)).Value.ShouldBeNull(); }

        foreach (var record in kept) { (await h.Store.FindAsync(record.Id)).Value.ShouldNotBeNull(); }

        (await h.Store.FindAsync(other.Record.Id)).Value.ShouldNotBeNull();
        (await h.Store.FindAsync(later.Record.Id)).Value.ShouldNotBeNull();
        (await h.Provider.Runtime.GetActiveSessionAsync()).Value.ShouldNotBeNull();
        (await h.Store.AppendAsync([old[0]])).Value.Records.Single().Outcome.ShouldBe(ProfilingWriteOutcome.Cleared);
        (await h.Store.AppendAsync([old[5]])).Value.Records.Single().Outcome.ShouldBe(ProfilingWriteOutcome.AlreadyStored);
        (await h.Store.QueryAsync(selection with { Cursor = page.Value.NextCursor })).Errors.ShouldContain(error => error is ProfilingQueryBoundaryError);
    }

    /// <summary>Unsafe or malformed group selections fail before clear coordination changes writer generations or retained history.</summary>
    /// <example><code>await suite.Clear_OperationGroup_InvalidSelection_ReturnsValidation("key");</code></example>
    [Theory]
    [InlineData("key")]
    [InlineData("kind")]
    [InlineData("contains")]
    [InlineData("range")]
    [InlineData("dataset")]
    [InlineData("cursor")]
    [InlineData("dimension")]
    public async Task Clear_OperationGroup_InvalidSelection_ReturnsValidation(string invalid)
    {
        var h = await this.CreateAsync();
        var record = h.Record();
        await h.Store.AppendAsync([h.Envelope(record, 1)]);
        var query = h.Query() with { Key = "Report", Kind = "Service" };
        query = invalid switch
        {
            "key" => query with { Key = null }, "kind" => query with { Kind = null },
            "contains" => query with { KeyContains = "port" }, "range" => query with { FromUtc = null, ToUtc = null },
            "cursor" => query with { Cursor = "page" },
            "dimension" => query with { Dimensions = [new() { Key = "batch", Operator = ProfilingDimensionOperator.Equal }] },
            _ => query,
        };
        var result = await h.Provider.ClearAsync(new() { DataSet = invalid == "dataset" ? ProfilingDataSet.All : ProfilingDataSet.Operations, OperationSelection = query });
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(error => error is ProfilingValidationError);
        (await h.Store.FindAsync(record.Id)).Value.ShouldNotBeNull();
    }

    [Fact]
    public async Task Clear_TwoWriters_FixesCutoffsAndPreservesNewRegistrations()
    {
        // Arrange
        var h = await this.CreateAsync();
        var second = (await h.Store.OpenWriterAsync(new() { AttemptId = Guid.NewGuid(), Node = h.Node })).Value;
        var old = h.Envelope(h.Record(), 1);
        var aboveCutoff = h.Envelope(h.Record(), 2);
        await h.Store.AppendAsync([old, aboveCutoff]);
        var clearing = h.Provider.ClearAsync(new() { DataSet = ProfilingDataSet.Operations });
        var pending = await PendingAsync(h, h.Lease);
        var laterLease = (await h.Store.OpenWriterAsync(new() { AttemptId = Guid.NewGuid(), Node = h.Node })).Value;
        var newer = h.Envelope(h.Record(), 1, laterLease);
        await h.Store.AppendAsync([newer]);

        // Act
        var firstAck = await AckAsync(h, pending.Id, h.Lease, 1);
        var retry = await AckAsync(h, pending.Id, h.Lease, 100);
        await AckAsync(h, pending.Id, second, 0);
        var result = await clearing.WaitAsync(TimeSpan.FromSeconds(5));
        var delayed = await h.Store.AppendAsync([old]);

        // Assert
        firstAck.Value.CompletionCutoff.ShouldBe(1);
        retry.Value.CompletionCutoff.ShouldBe(1);
        result.IsSuccess.ShouldBeTrue();
        result.Value.State.ShouldBe(ProfilingClearState.Completed);
        result.Value.DataSet.ShouldBe(ProfilingDataSet.Operations);
        result.Value.RemovedOperationCount.ShouldBe(1);
        result.Value.ProviderScope.ShouldBe(h.Provider.Capabilities.Scope);
        delayed.Value.Records.Single().Outcome.ShouldBe(ProfilingWriteOutcome.Cleared);
        (await h.Store.FindAsync(aboveCutoff.Record.Id)).Value.ShouldNotBeNull();
        (await h.Store.FindAsync(newer.Record.Id)).Value.ShouldNotBeNull();
    }

    [Fact]
    public async Task Clear_AcknowledgedThenExpiredWriter_PreservesRecordsAboveItsCutoff()
    {
        // Arrange
        var h = await this.CreateAsync();
        var shortLease = (await h.Store.OpenWriterAsync(new() { AttemptId = Guid.NewGuid(), Node = h.Node, LeaseDuration = TimeSpan.FromSeconds(1) })).Value;
        var older = h.Envelope(h.Record(), 1, shortLease);
        var newer = h.Envelope(h.Record(), 2, shortLease);
        await h.Store.AppendAsync([older, newer]);
        var clearing = h.Provider.ClearAsync(new() { DataSet = ProfilingDataSet.Operations });
        var pending = await PendingAsync(h, h.Lease);
        await AckAsync(h, pending.Id, shortLease, 1);
        h.Clock.Advance(TimeSpan.FromSeconds(2));

        // Act
        await AckAsync(h, pending.Id, h.Lease, 0);
        var result = await clearing.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        result.Value.RemovedOperationCount.ShouldBe(1);
        (await h.Store.FindAsync(older.Record.Id)).Value.ShouldBeNull();
        (await h.Store.FindAsync(newer.Record.Id)).Value.ShouldNotBeNull();
        (await h.Store.AppendAsync([older])).Value.Records.Single().Outcome.ShouldBe(ProfilingWriteOutcome.StaleWriter);
    }

    [Fact]
    public async Task Clear_UnacknowledgedExpiredWriter_SelectsItsWholeOldGeneration()
    {
        // Arrange
        var h = await this.CreateAsync();
        var shortLease = (await h.Store.OpenWriterAsync(new() { AttemptId = Guid.NewGuid(), Node = h.Node, LeaseDuration = TimeSpan.FromSeconds(1) })).Value;
        var record = h.Envelope(h.Record(), 20, shortLease);
        await h.Store.AppendAsync([record]);
        var clearing = h.Provider.ClearAsync(new() { DataSet = ProfilingDataSet.Operations });
        var pending = await PendingAsync(h, h.Lease);
        h.Clock.Advance(TimeSpan.FromSeconds(2));

        // Act
        await AckAsync(h, pending.Id, h.Lease, 0);
        var result = await clearing.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        result.Value.RemovedOperationCount.ShouldBe(1);
        (await h.Store.FindAsync(record.Record.Id)).Value.ShouldBeNull();
    }

    [Fact]
    public async Task Clear_HalfOpenCompletionRange_DeletesOwnedGraphOnly()
    {
        // Arrange
        var h = await this.CreateAsync();
        var from = h.Clock.GetUtcNow().AddSeconds(-10);
        var to = h.Clock.GetUtcNow();
        var lower = h.Record() with { StartedUtc = from.AddMinutes(-1), CompletedUtc = from };
        var upper = h.Record() with { StartedUtc = from, CompletedUtc = to };
        var before = h.Record() with { CompletedUtc = from.AddTicks(-1) };
        await h.Store.AppendAsync([h.Envelope(lower, 1), h.Envelope(upper, 2), h.Envelope(before, 3)]);
        var clearing = h.Provider.ClearAsync(new() { DataSet = ProfilingDataSet.Operations, FromUtc = from, ToUtc = to });
        var pending = await PendingAsync(h, h.Lease);

        // Act
        await AckAsync(h, pending.Id, h.Lease, 3);
        var result = await clearing.WaitAsync(TimeSpan.FromSeconds(5));
        var unmatchedRetry = await h.Store.AppendAsync([h.Envelope(upper, 2)]);

        // Assert
        result.Value.FromUtc.ShouldBe(from);
        result.Value.ToUtc.ShouldBe(to);
        result.Value.RemovedOperationCount.ShouldBe(1);
        (await h.Store.FindAsync(lower.Id)).Value.ShouldBeNull();
        (await h.Store.FindAsync(upper.Id)).Value.ShouldNotBeNull();
        (await h.Store.FindAsync(before.Id)).Value.ShouldNotBeNull();
        unmatchedRetry.Value.Records.Single().Outcome.ShouldBe(ProfilingWriteOutcome.AlreadyStored);
    }

    [Fact]
    public async Task Clear_CanceledPreparation_ReleasesGateWithoutRemovingHistory()
    {
        // Arrange
        var h = await this.CreateAsync();
        var record = h.Record();
        await h.Store.AppendAsync([h.Envelope(record, 1)]);
        using var canceled = new CancellationTokenSource();
        var clearing = h.Provider.ClearAsync(new() { DataSet = ProfilingDataSet.All }, canceled.Token);
        var pending = await PendingAsync(h, h.Lease);

        // Act
        canceled.Cancel();
        var result = await clearing.WaitAsync(TimeSpan.FromSeconds(5));
        var late = await AckAsync(h, pending.Id, h.Lease, 1);
        var runtimeStart = await h.Provider.Runtime.GetOrCreateActiveSessionAsync(RuntimeRequest(h));

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Value.State.ShouldBe(ProfilingClearState.Failed);
        result.Value.SealedUtc.ShouldBeNull();
        late.IsFailure.ShouldBeTrue();
        (await h.Store.FindAsync(record.Id)).Value.ShouldNotBeNull();
        runtimeStart.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Clear_RuntimeInclusiveActiveGuard_RejectsBeforeOperationMutation()
    {
        // Arrange
        var h = await this.CreateAsync();
        var start = await h.Provider.Runtime.GetOrCreateActiveSessionAsync(RuntimeRequest(h));
        start.IsSuccess.ShouldBeTrue();
        var record = h.Record();
        await h.Store.AppendAsync([h.Envelope(record, 1)]);

        // Act
        var all = await h.Provider.ClearAsync(new() { DataSet = ProfilingDataSet.All });
        var clearing = h.Provider.ClearAsync(new() { DataSet = ProfilingDataSet.Operations });
        var pending = await PendingAsync(h, h.Lease);
        await AckAsync(h, pending.Id, h.Lease, 1);
        var operations = await clearing.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        all.IsFailure.ShouldBeTrue();
        all.Errors.ShouldContain(e => e is ProfilingBusyError);
        operations.Value.RemovedOperationCount.ShouldBe(1);
        (await h.Provider.Runtime.GetActiveSessionAsync()).Value.ShouldNotBeNull();
    }

    [Fact]
    public async Task Clear_SealedBoundedDeletion_RecoversAndKeepsDelayedWritesFenced()
    {
        // Arrange
        var options = new ProfilingOptions();
        options.Storage.MaximumMaintenanceRoots = 1;
        var h = await this.CreateAsync(options);
        var first = h.Envelope(h.Record(), 1);
        var second = h.Envelope(h.Record(), 2);
        await h.Store.AppendAsync([first, second]);
        var clearing = h.Provider.ClearAsync(new() { DataSet = ProfilingDataSet.Operations });
        var pending = await PendingAsync(h, h.Lease);
        await AckAsync(h, pending.Id, h.Lease, 2);

        // Act
        var partial = await clearing.WaitAsync(TimeSpan.FromSeconds(5));
        var delayed = await h.Store.AppendAsync([first]);
        var recovered = await h.Provider.ResumeMaintenanceAsync(new() { MaximumRoots = 1 });

        // Assert
        partial.IsSuccess.ShouldBeTrue();
        partial.Value.State.ShouldBe(ProfilingClearState.Applying);
        partial.Value.HasRemainingWork.ShouldBeTrue();
        partial.Value.RemovedOperationCount.ShouldBe(1);
        delayed.Value.Records.Single().Outcome.ShouldBe(ProfilingWriteOutcome.Cleared);
        recovered.Value.RemovedOperations.ShouldBe(1);
        recovered.Value.RemainingClears.ShouldBe(0);
        (await h.Store.FindAsync(first.Record.Id)).Value.ShouldBeNull();
        (await h.Store.FindAsync(second.Record.Id)).Value.ShouldBeNull();
    }

    [Theory]
    [InlineData("one-bound")]
    [InlineData("backward")]
    [InlineData("local")]
    [InlineData("dataset")]
    public async Task Clear_InvalidSelection_ReturnsValidationBeforeMutation(string invalid)
    {
        // Arrange
        var h = await this.CreateAsync();
        var now = h.Clock.GetUtcNow();
        var selection = invalid switch
        {
            "one-bound" => new ProfilingClearRequest { FromUtc = now },
            "backward" => new ProfilingClearRequest { FromUtc = now, ToUtc = now.AddSeconds(-1) },
            "local" => new ProfilingClearRequest { FromUtc = now.ToOffset(TimeSpan.FromHours(1)), ToUtc = now.AddSeconds(1) },
            _ => new ProfilingClearRequest { DataSet = (ProfilingDataSet)100 },
        };

        // Act
        var result = await h.Provider.ClearAsync(selection);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(e => e is ProfilingValidationError);
    }

    private static RuntimeProfilingSessionCreateRequest RuntimeRequest(Harness h) =>
        new(ProfilingIdentityFactory.CreateRuntimeSession(), "contract", h.Clock.GetUtcNow(), TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1), []);

    private static async Task<ProfilingPendingClear> PendingAsync(Harness h, ProfilingWriterLease lease)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (true)
        {
            var state = await h.Store.SynchronizeWriterAsync(new() { Lease = lease }, deadline.Token);
            state.IsSuccess.ShouldBeTrue();
            if (state.Value.PendingClears.Count > 0)
            {
                return state.Value.PendingClears.ShouldHaveSingleItem();
            }

            await Task.Delay(10, deadline.Token);
        }
    }

    private static Task<IResult<ProfilingClearAcknowledgement>> AckAsync(Harness h, Guid id, ProfilingWriterLease lease, long cutoff) =>
        h.Store.AcknowledgeClearAsync(new() { ClearId = id, Lease = lease, CompletionCutoff = cutoff });
}
