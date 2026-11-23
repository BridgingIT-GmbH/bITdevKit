// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.Durable;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

[IntegrationTest("Infrastructure")]
public class LiteDbJobStoreProviderTests
{
    [Fact]
    public async Task RuntimeAndTriggerStates_UpsertListAndRemove_AreDetachedAndCaseInsensitive()
    {
        await using var host = new LiteDbDurableTestHost();
        var sut = host.Jobs;
        await sut.RuntimeStates.UpsertAsync(new() { JobName = "Ledger", Enabled = true });
        (await sut.RuntimeStates.GetAsync("ledger")).Enabled.ShouldBe(true);
        await sut.TriggerRuntimeStates.UpsertAsync("Ledger", "manual", new JobTriggerRuntimeState(null, null, null, false, Paused: true));
        (await sut.TriggerRuntimeStates.GetAsync("ledger", "MANUAL")).Paused.ShouldBeTrue();
        (await sut.RuntimeStates.ListAsync()).Count.ShouldBe(1);
        (await sut.TriggerRuntimeStates.ListAsync()).Single().JobName.ShouldBe("Ledger");
        await sut.RuntimeStates.RemoveAsync("ledger");
        await sut.TriggerRuntimeStates.RemoveAsync("ledger", "manual");
        (await sut.RuntimeStates.ListAsync()).ShouldBeEmpty();
        (await sut.TriggerRuntimeStates.ListAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Occurrences_CompetingKeysAndIds_AcceptOnlyOneOwner()
    {
        await using var host = new LiteDbDurableTestHost();
        var sut = host.Jobs;
        var occurrence = CreateOccurrence("same", host.Clock.UtcNow);
        var outcomes = await Task.WhenAll(sut.Occurrences.TryCreateAsync(occurrence), sut.Occurrences.TryCreateAsync(CreateOccurrence("same", host.Clock.UtcNow)));
        outcomes.Count(x => x).ShouldBe(1);
        var stored = await sut.Occurrences.GetByKeyAsync("SAME");
        (await sut.Occurrences.TryCreateAsync(occurrence with { OccurrenceId = stored.OccurrenceId, OccurrenceKey = "other" })).ShouldBeFalse();
        var updated = stored with { Status = JobOccurrenceStatus.Completed };
        await sut.Occurrences.UpdateAsync(updated);
        (await sut.Queries.ListOccurrencesAsync()).Single().Status.ShouldBe(JobOccurrenceStatus.Completed);
        await sut.Occurrences.RemoveAsync(stored.OccurrenceId);
        (await sut.Occurrences.ListAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task OccurrencePayloadsAndAcceptedEvents_RoundTripDeclaredTypesAndCursorOrder()
    {
        await using var host = new LiteDbDurableTestHost();
        var sut = host.Jobs;
        var occurrence = CreateOccurrence("payload", host.Clock.UtcNow) with { Data = new DurableJobData { Value = "stored" }, DataType = typeof(DurableJobData) };
        await sut.Occurrences.TryCreateAsync(occurrence);
        ((DurableJobData)(await sut.Occurrences.GetAsync(occurrence.OccurrenceId)).Data).Value.ShouldBe("stored");
        var accepted = new JobAcceptedEvent { AcceptedEventId = Guid.NewGuid(), Source = "ledger", IdempotencyKey = "one", Data = new DurableJobData { Value = "event" }, DataType = typeof(DurableJobData), AcceptedUtc = host.Clock.UtcNow };
        (await sut.AcceptedEvents.TryAcceptAsync(accepted)).ShouldBeTrue();
        (await sut.AcceptedEvents.TryAcceptAsync(accepted with { AcceptedEventId = Guid.NewGuid() })).ShouldBeFalse();
        var rows = await sut.AcceptedEvents.ListPendingAsync("ledger", typeof(DurableJobData), null, null, 10);
        ((DurableJobData)rows.Single().Data).Value.ShouldBe("event");
        (await sut.AcceptedEvents.ListPendingAsync("ledger", typeof(DurableJobData), accepted.AcceptedUtc, accepted.AcceptedEventId, 10)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Lease_ExpiredAndReclaimed_RejectsStaleRenewalAndRelease()
    {
        await using var host = new LiteDbDurableTestHost();
        var sut = host.Jobs.Leases;
        var id = Guid.NewGuid();
        var lease = await sut.TryAcquireAsync(id, "first", TimeSpan.FromMinutes(1));
        (await sut.TryAcquireAsync(id, "second", TimeSpan.FromMinutes(1))).ShouldBeNull();
        host.Clock.Advance(TimeSpan.FromMinutes(2));
        var replacement = await sut.TryAcquireAsync(id, "second", TimeSpan.FromMinutes(1));
        replacement.OwnershipToken.ShouldNotBe(lease.OwnershipToken);
        (await sut.RenewAsync(id, "first", lease.OwnershipToken, TimeSpan.FromMinutes(1))).ShouldBeNull();
        (await sut.ReleaseAsync(id, "first", lease.OwnershipToken)).ShouldBeFalse();
        (await sut.VerifyOwnershipAsync(id, "second", replacement.OwnershipToken)).ShouldBeTrue();
        host.Clock.Advance(TimeSpan.FromMinutes(2));
        (await sut.ListExpiredAsync(host.Clock.UtcNow)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task DependenciesAndHistories_UpdateAndPurge_PersistAcrossCommands()
    {
        await using var host = new LiteDbDurableTestHost();
        var sut = host.Jobs;
        var dependent = Guid.NewGuid();
        var prerequisite = Guid.NewGuid();
        var dependency = new JobOccurrenceDependency { DependencyId = Guid.NewGuid(), DependentOccurrenceId = dependent, PrerequisiteOccurrenceId = prerequisite };
        await sut.Dependencies.AddAsync(dependency);
        await sut.Dependencies.UpdateAsync(dependency with { Status = JobDependencyStatus.Satisfied });
        (await sut.Dependencies.ListByDependentAsync(dependent)).Single().Status.ShouldBe(JobDependencyStatus.Satisfied);
        (await sut.Dependencies.ListByPrerequisiteAsync(prerequisite)).Count.ShouldBe(1);
        var history = new JobExecutionHistoryEntry { HistoryId = Guid.NewGuid(), OccurrenceId = dependent, RecordedAt = host.Clock.UtcNow };
        await sut.ExecutionHistory.AppendAsync(history);
        (await sut.ExecutionHistory.ListAsync(dependent)).Count.ShouldBe(1);
        (await sut.Queries.ListExecutionHistoryAsync()).Count.ShouldBe(1);
        (await sut.ExecutionHistory.PurgeAsync(host.Clock.UtcNow.AddMinutes(1), [history.HistoryId])).ShouldBe(1);
        (await sut.Dependencies.RemoveByOccurrenceAsync(dependent)).ShouldBeGreaterThan(0);
        (await sut.Queries.ListDependenciesAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task BatchWithChildren_ConflictingChild_RollsBackAllMembershipAndChildChanges()
    {
        await using var host = new LiteDbDurableTestHost();
        var sut = host.Jobs;
        var existing = CreateOccurrence("owned", host.Clock.UtcNow);
        await sut.Occurrences.TryCreateAsync(existing);
        var batch = new JobBatch { BatchId = Guid.NewGuid(), CreatedDate = host.Clock.UtcNow };
        var good = CreateOccurrence("new", host.Clock.UtcNow);
        var bad = existing with { OccurrenceId = Guid.NewGuid() };
        (await sut.Batches.TryCreateWithChildrenAsync(batch, [good, bad], [])).ShouldBeFalse();
        (await sut.Batches.GetAsync(batch.BatchId)).ShouldBeNull();
        (await sut.Occurrences.GetByKeyAsync("new")).ShouldBeNull();
        (await sut.Batches.TryCreateWithChildrenAsync(batch, [good], [new JobBatchOccurrence { BatchId = batch.BatchId, OccurrenceId = good.OccurrenceId }])).ShouldBeTrue();
        (await sut.Batches.ListOccurrencesAsync(batch.BatchId)).Count.ShouldBe(1);
        await sut.BatchHistory.AppendAsync(new() { HistoryId = Guid.NewGuid(), BatchId = batch.BatchId, RecordedAt = host.Clock.UtcNow });
        (await sut.Queries.ListBatchHistoryAsync()).Count.ShouldBe(1);
        (await sut.Queries.ListBatchOccurrencesAsync()).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Reopen_PreviousExecutionAndOccurrence_PreserveCommittedHistory()
    {
        var directory = Directory.CreateTempSubdirectory("bdk-jobs-reopen-");
        try
        {
            var file = Path.Combine(directory.FullName, "jobs.db");
            var occurrence = CreateOccurrence("durable", DateTimeOffset.UtcNow);
            var execution = new JobExecution { ExecutionId = Guid.NewGuid(), OccurrenceId = occurrence.OccurrenceId, JobName = occurrence.JobName, TriggerName = occurrence.TriggerName,
                AttemptNumber = 1, Status = JobExecutionStatus.Completed, StartedUtc = DateTimeOffset.UtcNow.AddMinutes(-10), CompletedUtc = DateTimeOffset.UtcNow.AddMinutes(-9) };
            await using (var first = new LiteDbDurableTestHost(file: file))
            {
                await first.Jobs.Occurrences.TryCreateAsync(occurrence);
                await first.Jobs.Executions.CreateAsync(execution);
            }

            await using var reopened = new LiteDbDurableTestHost(file: file);
            var sut = reopened.Jobs;
            (await sut.Occurrences.GetAsync(occurrence.OccurrenceId)).OccurrenceKey.ShouldBe("durable");
            (await sut.PreviousExecutions.GetPreviousSuccessfulExecutionAsync(occurrence.JobName, occurrence.TriggerName, DateTimeOffset.UtcNow)).ExecutionId.ShouldBe(execution.ExecutionId);
            (await sut.Queries.ListExecutionsAsync()).Count.ShouldBe(1);
            (await sut.Executions.RemoveByOccurrenceAsync(occurrence.OccurrenceId)).ShouldBe(1);
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public async Task Batch_DuplicateExternalIdentityAndConflictingInputKeys_AreRejectedAtomically()
    {
        await using var host = new LiteDbDurableTestHost();
        var sut = host.Jobs;
        var batch = new JobBatch { BatchId = Guid.NewGuid(), ExternalBatchId = "statement" };
        (await sut.Batches.TryCreateAsync(batch, [])).ShouldBeTrue();
        (await sut.Batches.TryCreateAsync(batch with { BatchId = Guid.NewGuid() }, [])).ShouldBeFalse();
        var children = new[] { CreateOccurrence("duplicate-input", host.Clock.UtcNow), CreateOccurrence("duplicate-input", host.Clock.UtcNow) };
        var invalid = new JobBatch { BatchId = Guid.NewGuid(), ExternalBatchId = "invalid" };
        (await sut.Batches.TryCreateWithChildrenAsync(invalid, children, [])).ShouldBeFalse();
        (await sut.Occurrences.ListAsync()).ShouldBeEmpty();
        (await sut.Batches.ListAsync()).Count.ShouldBe(1);
    }

    [Fact]
    public async Task ExistingScheduler_DispatchAndSweep_UsesNativeOccurrenceExecutionAndBatchStores()
    {
        await using var host = new LiteDbDurableTestHost(s => s.AddSingleton(Substitute.For<IHostApplicationLifetime>()), configureJobs: jobs => jobs
            .WithBackgroundExecution(o => o.EnableBackgroundExecution = false)
            .WithJob<DurableSuccessfulJob>("durable-job", job => job.AddTrigger("manual", trigger => trigger.Manual())));
        var scheduler = host.Services.GetRequiredService<IJobSchedulerService>();
        var dispatched = await scheduler.DispatchBatchAsync(new JobBatchDispatchRequest
        {
            BatchId = "runtime-proof", Items = [new JobBatchDispatchItem { JobName = "durable-job", Data = Unit.Value }]
        });
        dispatched.IsSuccess.ShouldBeTrue(string.Join("; ", dispatched.Errors.Select(e => e.Message)));
        var background = host.Services.GetRequiredService<JobSchedulerBackgroundService>();
        for (var i = 0; i < 4; i++) { await background.SweepOnceAsync(); }

        (await host.Jobs.Queries.ListExecutionsAsync()).ShouldNotBeEmpty();
        (await host.Jobs.Batches.ListAsync()).Single().ExternalBatchId.ShouldBe("runtime-proof");
    }

    private static JobOccurrence CreateOccurrence(string key, DateTimeOffset due) => new()
    {
        OccurrenceId = Guid.NewGuid(), OccurrenceKey = key, JobName = "ledger", TriggerName = "manual", TriggerType = JobTriggerType.Manual,
        Status = JobOccurrenceStatus.Pending, DueUtc = due, CreatedDate = due, UpdatedDate = due
    };
}

public class DurableJobData
{
    public string Value { get; set; }
}

public sealed class DurableSuccessfulJob : JobBase
{
    public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default) => Task.FromResult(Result.Success());
}
