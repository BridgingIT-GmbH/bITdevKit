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
        var state = await h.Store.SynchronizeWriterAsync(new() { Lease = lease });
        return state.Value.PendingClears.ShouldHaveSingleItem();
    }

    private static Task<IResult<ProfilingClearAcknowledgement>> AckAsync(Harness h, Guid id, ProfilingWriterLease lease, long cutoff) =>
        h.Store.AcknowledgeClearAsync(new() { ClearId = id, Lease = lease, CompletionCutoff = cutoff });
}
