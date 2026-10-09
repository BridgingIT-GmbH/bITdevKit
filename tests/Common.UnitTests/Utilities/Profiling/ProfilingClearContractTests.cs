// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

using Microsoft.Extensions.Time.Testing;

public sealed class ProfilingClearContractTests
{
    [Fact]
    public async Task Clear_DeadlineExpires_UnreachableWriterCannotDeleteOrSealLater()
    {
        // Arrange
        var (sut, clock, lease, record) = await CreateAsync();
        await sut.AppendAsync([new() { Lease = lease, CompletionSequence = 1, Record = record }]);
        var clearing = sut.ClearAsync(new() { DataSet = ProfilingDataSet.Operations });
        var pending = (await sut.SynchronizeWriterAsync(new() { Lease = lease })).Value.PendingClears.Single();

        // Act
        clock.Advance(TimeSpan.FromSeconds(11));
        var result = await clearing.WaitAsync(TimeSpan.FromSeconds(5));
        var late = await sut.AcknowledgeClearAsync(new() { ClearId = pending.Id, Lease = lease, CompletionCutoff = 1 });

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(e => e is ProfilingBusyError);
        result.Value.State.ShouldBe(ProfilingClearState.Failed);
        late.IsFailure.ShouldBeTrue();
        (await sut.FindAsync(record.Id)).Value.ShouldNotBeNull();
        (await sut.SynchronizeWriterAsync(new() { Lease = lease })).Value.PendingClears.ShouldBeEmpty();
    }

    [Fact]
    public async Task Maintenance_ExpiredPreparation_ReleasesRuntimeGateWithoutDeletingHistory()
    {
        // Arrange
        var (sut, clock, lease, record) = await CreateAsync();
        await sut.AppendAsync([new() { Lease = lease, CompletionSequence = 1, Record = record }]);
        var clearing = sut.ClearAsync(new() { DataSet = ProfilingDataSet.All });
        var during = await sut.Runtime.GetOrCreateActiveSessionAsync(Request(clock));

        // Act
        clock.Advance(TimeSpan.FromSeconds(11));
        await sut.ResumeMaintenanceAsync(new());
        var result = await clearing.WaitAsync(TimeSpan.FromSeconds(5));
        var after = await sut.Runtime.GetOrCreateActiveSessionAsync(Request(clock));

        // Assert
        during.IsFailure.ShouldBeTrue();
        during.Errors.ShouldContain(e => e is ProfilingBusyError);
        result.Value.SealedUtc.ShouldBeNull();
        (await sut.FindAsync(record.Id)).Value.ShouldNotBeNull();
        after.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Clear_FenceCapacity_IsBoundedUntilOriginalWriterSettles()
    {
        // Arrange
        var (sut, _, lease, record) = await CreateAsync(options => options.MaximumClearFences = 1);
        var first = sut.ClearAsync(new() { DataSet = ProfilingDataSet.Operations });
        var pending = (await sut.SynchronizeWriterAsync(new() { Lease = lease })).Value.PendingClears.Single();
        await sut.AcknowledgeClearAsync(new() { ClearId = pending.Id, Lease = lease, CompletionCutoff = 1 });
        await first.WaitAsync(TimeSpan.FromSeconds(5));

        // Act
        var blocked = await sut.ClearAsync(new() { DataSet = ProfilingDataSet.Operations });
        await sut.SynchronizeWriterAsync(new() { Lease = lease, SettledThrough = 1 });
        var second = sut.ClearAsync(new() { DataSet = ProfilingDataSet.Operations });
        var secondPending = (await sut.SynchronizeWriterAsync(new() { Lease = lease })).Value.PendingClears.Single();
        await sut.AcknowledgeClearAsync(new() { ClearId = secondPending.Id, Lease = lease, CompletionCutoff = 1 });
        var completed = await second.WaitAsync(TimeSpan.FromSeconds(5));
        var settledRetry = await sut.AppendAsync([new() { Lease = lease, CompletionSequence = 1, Record = record }]);

        // Assert
        blocked.IsFailure.ShouldBeTrue();
        blocked.Errors.ShouldContain(e => e is ProfilingBusyError);
        completed.IsSuccess.ShouldBeTrue();
        settledRetry.Value.Records.Single().Outcome.ShouldBeOneOf(ProfilingWriteOutcome.Cleared, ProfilingWriteOutcome.AlreadySettled);
        (await sut.FindAsync(record.Id)).Value.ShouldBeNull();
    }

    [Fact]
    public async Task RuntimeClear_UsesRuntimeDataSet_AndPreservesPinnedSelectionSemantics()
    {
        // Arrange
        var (sut, clock, lease, record) = await CreateAsync();
        await sut.AppendAsync([new() { Lease = lease, CompletionSequence = 1, Record = record }]);
        var active = (await sut.Runtime.GetOrCreateActiveSessionAsync(Request(clock))).Value.Session;
        await sut.Runtime.TryTransitionSessionAsync(active.Identity.Id, [RuntimeProfilingSessionState.Running], RuntimeProfilingSessionState.Completed, clock.GetUtcNow());
        await sut.Runtime.UpdateSessionMetadataAsync(active.Identity.Key, new RuntimeProfilingSessionMetadata(null, [], null, true));

        // Act
        var result = await sut.Runtime.ClearAsync();
        var sessions = await sut.Runtime.ListSessionsAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.DataSet.ShouldBe(ProfilingDataSet.Runtime);
        result.Value.RemovedSessionCount.ShouldBe(1);
        sessions.Value.ShouldBeEmpty();
        (await sut.FindAsync(record.Id)).Value.ShouldNotBeNull();
    }

    [Fact]
    public async Task SharedNode_RuntimeAttachesToOperationIdentity_WithoutSeparateProcessIdentity()
    {
        // Arrange
        var (sut, clock, lease, record) = await CreateAsync();
        await sut.AppendAsync([new() { Lease = lease, CompletionSequence = 1, Record = record }]);
        var correlation = new RuntimeProfilingNodeCorrelation("host-node", clock.GetUtcNow());

        // Act
        var runtimeNode = await sut.Runtime.GetOrCreateNodeAsync(correlation, record.Node);
        var operations = await sut.FindAsync(record.Id);

        // Assert
        runtimeNode.IsSuccess.ShouldBeTrue();
        runtimeNode.Value.Identity.ShouldBe(operations.Value.Node.Identity);
        runtimeNode.Value.ProcessStartedUtc.ShouldBe(operations.Value.Node.ProcessStartedUtc);
        (await sut.Runtime.FindNodeAsync(correlation)).Value.Identity.ShouldBe(record.Node.Identity);
    }

    [Fact]
    public async Task Append_InvalidNewNodeDescriptor_DoesNotEvictValidHistory()
    {
        // Arrange
        var options = new ProfilingOptions();
        options.Operations.MaximumRetainedOperations = 1;
        var (profiler, sink, clock) = OperationProfilerTests.Create();
        using (var operation = profiler.BeginOperation("root")) { operation.Complete(); }

        var record = sink.Records.Single();
        var sut = new InMemoryProfilingStorageProvider(options, clock);
        var lease = (await sut.OpenWriterAsync(new() { AttemptId = Guid.NewGuid(), Node = record.Node })).Value;
        await sut.AppendAsync([new() { Lease = lease, CompletionSequence = 1, Record = record }]);
        await sut.SynchronizeWriterAsync(new() { Lease = lease, SettledThrough = 1 });
        var malformed = record with { Id = Guid.NewGuid(), Node = record.Node with { HostName = "wrong-process" } };

        // Act
        var rejected = await sut.AppendAsync([new() { Lease = lease, CompletionSequence = 2, Record = malformed }]);

        // Assert
        rejected.Value.Records.Single().SafeCode.ShouldBe("NodeIdentityConflict");
        (await sut.FindAsync(record.Id)).Value.ShouldNotBeNull();
    }

    /// <summary>Checks that append-pressure evictions are reported once without consuming later maintenance work limits.</summary>
    /// <example>Executed by the focused Profiling suite.</example>
    [Fact]
    public async Task Maintenance_AppendCapacityEvictions_ReportsOnceSeparatelyFromWorkBudget()
    {
        var options = new ProfilingOptions();
        options.Operations.MaximumRetainedOperations = 1;
        options.Storage.MaximumMaintenanceRoots = 1;
        var (profiler, sink, clock) = OperationProfilerTests.Create();
        using (var operation = profiler.BeginOperation("root")) { operation.Complete(); }

        var record = sink.Records.Single();
        var sut = new InMemoryProfilingStorageProvider(options, clock);
        var lease = (await sut.OpenWriterAsync(new() { AttemptId = Guid.NewGuid(), Node = record.Node })).Value;
        for (var sequence = 1; sequence <= 3; sequence++)
        {
            var appended = await sut.AppendAsync([new() { Lease = lease, CompletionSequence = sequence, Record = record with { Id = Guid.NewGuid() } }]);
            appended.Value.Records.Single().Outcome.ShouldBe(ProfilingWriteOutcome.Accepted);
            await sut.SynchronizeWriterAsync(new() { Lease = lease, SettledThrough = sequence });
        }

        var request = new ProfilingMaintenanceRequest { MaximumRoots = 1, MaximumOperationCount = 1 };
        var first = (await sut.ResumeMaintenanceAsync(request)).Value;
        first.CapacityEvictedOperations.ShouldBe(2);
        first.RetentionRemovedOperations.ShouldBe(0);
        first.RemovedOperations.ShouldBe(0);
        (await sut.ResumeMaintenanceAsync(request)).Value.CapacityEvictedOperations.ShouldBe(0);

        clock.Advance(TimeSpan.FromDays(2));
        var expired = (await sut.ResumeMaintenanceAsync(request)).Value;
        expired.RetentionRemovedOperations.ShouldBe(1);
        expired.CapacityEvictedOperations.ShouldBe(0);
        expired.RemovedOperations.ShouldBe(1);
    }

    private static RuntimeProfilingSessionCreateRequest Request(TimeProvider clock) => new(ProfilingIdentityFactory.CreateRuntimeSession(), "runtime", clock.GetUtcNow(), TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1), []);

    private static async Task<(InMemoryProfilingStorageProvider Provider, FakeTimeProvider Clock, ProfilingWriterLease Lease, OperationProfilingRecord Record)> CreateAsync(Action<ProfilingStorageOptions> configure = null)
    {
        var (profiler, sink, clock) = OperationProfilerTests.Create();
        using (var operation = profiler.BeginOperation("root")) { operation.Complete(); }

        var record = sink.Records.Single();
        var options = new ProfilingOptions();
        configure?.Invoke(options.Storage);
        var provider = new InMemoryProfilingStorageProvider(options, clock);
        var lease = (await provider.OpenWriterAsync(new() { AttemptId = Guid.NewGuid(), Node = record.Node })).Value;
        return (provider, clock, lease, record);
    }
}
