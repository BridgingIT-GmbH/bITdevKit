// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.Durable;

using BridgingIT.DevKit.Application.Orchestrations;
using Microsoft.Extensions.DependencyInjection;

[IntegrationTest("Infrastructure")]
public class LiteDbOrchestrationStorageProviderTests
{
    [Fact]
    public async Task Snapshot_SaveAndRehydrate_PreservesDataAndRejectsStaleVersion()
    {
        await using var host = new LiteDbDurableTestHost();
        var sut = host.Orchestrations;
        var context = CreateContext(host);
        context.Properties["attempt"] = 3;
        var initial = await sut.Instances.CreateAsync(context, "ledger/one");
        context.Data.Value = "updated";
        context.CurrentState = "Approved";
        var current = await sut.Instances.SaveAsync(initial, context);
        current.Version.ShouldBe(initial.Version + 1);
        await Should.ThrowAsync<InvalidOperationException>(() => sut.Instances.SaveAsync(initial, context));
        var restored = await sut.Queries.GetContextAsync<DurableOrchestrationData>(context.InstanceId);
        restored.Data.Value.ShouldBe("updated");
        restored.CurrentState.ShouldBe("Approved");
        restored.Properties["attempt"].ShouldBe(3);
    }

    [Fact]
    public async Task ActiveConcurrencyKey_CompetingCreates_RejectsSecondUntilTerminal()
    {
        await using var host = new LiteDbDurableTestHost();
        var sut = host.Orchestrations;
        var first = CreateContext(host);
        var initial = await sut.Instances.CreateAsync(first, "same-key");
        await Should.ThrowAsync<InvalidOperationException>(() => sut.Instances.CreateAsync(CreateContext(host), "same-key"));
        first.Status = OrchestrationStatus.Completed;
        await sut.Instances.SaveAsync(initial, first);
        (await sut.Instances.CreateAsync(CreateContext(host), "same-key")).ShouldNotBeNull();
    }

    [Fact]
    public async Task Leases_ExpiryAndReplacement_FenceOldOwner()
    {
        await using var host = new LiteDbDurableTestHost();
        var sut = host.Orchestrations.Leases;
        var id = Guid.NewGuid();
        var first = await sut.AcquireAsync(id, "first", TimeSpan.FromMinutes(1));
        await Should.ThrowAsync<InvalidOperationException>(() => sut.AcquireAsync(id, "other", TimeSpan.FromMinutes(1)));
        host.Clock.Advance(TimeSpan.FromMinutes(2));
        var next = await sut.AcquireAsync(id, "other", TimeSpan.FromMinutes(1));
        await Should.ThrowAsync<InvalidOperationException>(() => sut.RenewAsync(id, first.LeaseId, "first", TimeSpan.FromMinutes(1)));
        await Should.ThrowAsync<InvalidOperationException>(() => sut.ReleaseAsync(id, first.LeaseId, "first"));
        (await sut.VerifyAsync(id, next.LeaseId, "other")).ShouldBeTrue();
    }

    [Fact]
    public async Task SignalsAndTimers_IdempotencyEligibilityAndStatus_AreDurable()
    {
        await using var host = new LiteDbDurableTestHost();
        var sut = host.Orchestrations;
        var context = CreateContext(host);
        await sut.Instances.CreateAsync(context);
        var first = await sut.Signals.PersistAsync(context.InstanceId, "approve", new DurableOrchestrationData { Value = "signal" }, "Awaiting", "once");
        var duplicate = await sut.Signals.PersistAsync(context.InstanceId, "approve", new DurableOrchestrationData { Value = "signal" }, "Awaiting", "once");
        duplicate.SignalId.ShouldBe(first.SignalId);
        (await sut.Signals.GetProcessableAsync(context.InstanceId, "Other")).ShouldBeEmpty();
        (await sut.Signals.GetProcessableAsync(context.InstanceId, "Awaiting")).Count.ShouldBe(1);
        await sut.Signals.UpdateStatusAsync(first.SignalId, OrchestrationSignalStatus.Processed);
        (await sut.Signals.GetProcessableAsync(context.InstanceId, "Awaiting")).ShouldBeEmpty();
        var due = await sut.Timers.ScheduleAsync(context.InstanceId, "timeout", host.Clock.UtcNow);
        await sut.Timers.ScheduleAsync(context.InstanceId, "later", host.Clock.UtcNow.AddHours(1));
        (await sut.Timers.GetDueAsync(host.Clock.UtcNow)).Single().TimerId.ShouldBe(due.TimerId);
        await sut.Timers.UpdateStatusAsync(due.TimerId, OrchestrationTimerStatus.Consumed);
        (await sut.Timers.GetDueAsync(host.Clock.UtcNow)).ShouldBeEmpty();
        (await sut.Queries.GetSignalsAsync(context.InstanceId)).Count.ShouldBe(1);
        (await sut.Queries.GetTimersAsync(context.InstanceId)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task HistoryQueriesMetricsAndAdministration_OperateOnPersistedRecords()
    {
        await using var host = new LiteDbDurableTestHost();
        var sut = host.Orchestrations;
        var context = CreateContext(host);
        await sut.Instances.CreateAsync(context, "key");
        await sut.History.AppendAsync(new() { InstanceId = context.InstanceId, EventType = "Created" });
        var query = await sut.Queries.QueryAsync(new() { OrchestrationName = "FictionalLedger", ConcurrencyKey = "key", Take = 1 });
        query.TotalCount.ShouldBe(1);
        (await sut.Queries.GetMetricsAsync()).WaitingInstances.ShouldBe(1);
        (await sut.Queries.GetHistoryAsync(context.InstanceId)).Single().EventType.ShouldBe("Created");
        (await sut.Administration.ArchiveAsync(context.InstanceId)).ShouldBeTrue();
        (await sut.Administration.ArchiveAsync(context.InstanceId)).ShouldBeFalse();
        (await sut.Administration.PurgeAsync(new() { IsArchived = true })).PurgedInstanceCount.ShouldBe(1);
        (await sut.Instances.GetAsync(context.InstanceId)).ShouldBeNull();
    }

    [Fact]
    public async Task Purge_ActiveLease_PreservesOwnedInstanceAndRecords()
    {
        await using var host = new LiteDbDurableTestHost();
        var sut = host.Orchestrations;
        var context = CreateContext(host);
        await sut.Instances.CreateAsync(context);
        await sut.Leases.AcquireAsync(context.InstanceId, "running", TimeSpan.FromMinutes(1));
        (await sut.Administration.PurgeAsync(new())).PurgedInstanceCount.ShouldBe(0);
        await sut.Administration.ReleaseLeaseAsync(context.InstanceId);
        (await sut.Administration.PurgeAsync(new())).PurgedInstanceCount.ShouldBe(1);
    }

    [Fact]
    public async Task Reopen_ContextSignalsAndTimers_RemainResumable()
    {
        var directory = Directory.CreateTempSubdirectory("bdk-orchestration-reopen-");
        try
        {
            var file = Path.Combine(directory.FullName, "workflow.db");
            Guid id;
            await using (var first = new LiteDbDurableTestHost(file: file))
            {
                var context = CreateContext(first);
                id = context.InstanceId;
                await first.Orchestrations.Instances.CreateAsync(context);
                await first.Orchestrations.Signals.PersistAsync(id, "resume", "ready");
                await first.Orchestrations.Timers.ScheduleAsync(id, "wake", first.Clock.UtcNow);
            }

            await using var reopened = new LiteDbDurableTestHost(file: file);
            var sut = reopened.Orchestrations;
            (await sut.Queries.GetContextAsync<DurableOrchestrationData>(id)).Data.Value.ShouldBe("initial");
            (await sut.Signals.GetAsync(id)).Count.ShouldBe(1);
            (await sut.Timers.GetDueAsync(reopened.Clock.UtcNow)).Count.ShouldBe(1);
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public async Task ExistingRuntime_ExecuteTransition_PersistsTerminalContextAndHistory()
    {
        await using var host = new LiteDbDurableTestHost(configureOrchestrations: builder => builder.WithOrchestration<DurableImmediateOrchestration>());
        var sut = host.Services.GetRequiredService<IOrchestrationService>();
        var result = await sut.ExecuteAsync<DurableImmediateOrchestration, DurableOrchestrationData>(new() { Value = "initial" });
        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
        var restored = await host.Orchestrations.Queries.GetContextAsync<DurableOrchestrationData>(result.Value.InstanceId);
        restored.Status.ShouldBe(OrchestrationStatus.Completed);
        restored.CurrentState.ShouldBe("Done");
        restored.Data.Value.ShouldBe("completed");
        (await host.Orchestrations.Queries.GetHistoryAsync(result.Value.InstanceId)).ShouldNotBeEmpty();
    }

    private static OrchestrationContext<DurableOrchestrationData> CreateContext(LiteDbDurableTestHost host) => new("FictionalLedger", new() { Value = "initial" }, host.Services, startedUtc: host.Clock.UtcNow)
    {
        Status = OrchestrationStatus.Waiting, CurrentState = "Awaiting"
    };
}

public class DurableOrchestrationData : IOrchestrationData
{
    public string Value { get; set; }
}

public class DurableImmediateOrchestration : Orchestration<DurableOrchestrationData>
{
    protected override void Define(IOrchestrationBuilder<DurableOrchestrationData> builder) => builder
        .State("Start", state => state.TransitionTo("Done"))
        .State("Done", state => state.Activity((context, _) =>
        {
            context.Data.Value = "completed";
            return Task.FromResult(OrchestrationOutcome.Continue());
        }).Complete());
}
