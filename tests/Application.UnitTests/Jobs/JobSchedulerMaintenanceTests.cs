// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Jobs;

using BridgingIT.DevKit.Application.Jobs;
using Microsoft.Extensions.DependencyInjection;

public class JobSchedulerMaintenanceTests(ITestOutputHelper output) : JobSchedulerTestBase(output)
{
    [Fact]
    public async Task ArchiveOccurrencesAsync_ArchivesEligibleTerminalOccurrences()
    {
        using var harness = this.CreateHarness(jobs => jobs
            .WithJob<MaintenanceTestJob>("archive-job", job => job
                .Description("Archive test job.")
                .AddTrigger("manual", trigger => trigger.Manual())));

        var store = harness.Services.GetRequiredService<IJobStoreProvider>();
        var maintenance = harness.Services.GetRequiredService<IJobSchedulerMaintenanceService>();
        var nowUtc = harness.Clock.GetUtcNow();
        var archiveOccurrenceId = Guid.NewGuid();
        var recentOccurrenceId = Guid.NewGuid();

        (await store.Occurrences.TryCreateAsync(new JobOccurrence
        {
            OccurrenceId = archiveOccurrenceId,
            OccurrenceKey = "archive-job:manual:old-completed",
            JobName = "archive-job",
            TriggerName = "manual",
            TriggerType = JobTriggerType.Manual,
            Status = JobOccurrenceStatus.Completed,
            DueUtc = nowUtc.AddDays(-3),
            CreatedDate = nowUtc.AddDays(-3),
            UpdatedDate = nowUtc.AddDays(-2),
            Data = Unit.Value,
            DataType = typeof(Unit),
        })).ShouldBeTrue();

        (await store.Occurrences.TryCreateAsync(new JobOccurrence
        {
            OccurrenceId = recentOccurrenceId,
            OccurrenceKey = "archive-job:manual:recent-completed",
            JobName = "archive-job",
            TriggerName = "manual",
            TriggerType = JobTriggerType.Manual,
            Status = JobOccurrenceStatus.Completed,
            DueUtc = nowUtc.AddHours(-3),
            CreatedDate = nowUtc.AddHours(-3),
            UpdatedDate = nowUtc.AddHours(-2),
            Data = Unit.Value,
            DataType = typeof(Unit),
        })).ShouldBeTrue();

        var result = await maintenance.ArchiveOccurrencesAsync(new JobArchiveOccurrencesJobData
        {
            RetentionWindow = TimeSpan.FromDays(1),
            JobName = "archive-job",
            BatchSize = 1,
        });

        result.Operation.ShouldBe("jobs-archive-occurrences");
        result.MatchedCount.ShouldBe(1);
        result.ProcessedCount.ShouldBe(1);
        (await store.Occurrences.GetAsync(archiveOccurrenceId)).Status.ShouldBe(JobOccurrenceStatus.Archived);
        (await store.Occurrences.GetAsync(recentOccurrenceId)).Status.ShouldBe(JobOccurrenceStatus.Completed);
    }

    [Fact]
    public async Task ArchiveOccurrencesAsync_DryRun_LeavesOccurrencesUntouched()
    {
        using var harness = this.CreateHarness(jobs => jobs
            .WithJob<MaintenanceTestJob>("archive-job", job => job
                .Description("Archive test job.")
                .AddTrigger("manual", trigger => trigger.Manual())));

        var store = harness.Services.GetRequiredService<IJobStoreProvider>();
        var maintenance = harness.Services.GetRequiredService<IJobSchedulerMaintenanceService>();
        var nowUtc = harness.Clock.GetUtcNow();
        var occurrenceId = Guid.NewGuid();

        (await store.Occurrences.TryCreateAsync(new JobOccurrence
        {
            OccurrenceId = occurrenceId,
            OccurrenceKey = "archive-job:manual:dry-run",
            JobName = "archive-job",
            TriggerName = "manual",
            TriggerType = JobTriggerType.Manual,
            Status = JobOccurrenceStatus.Failed,
            DueUtc = nowUtc.AddDays(-3),
            CreatedDate = nowUtc.AddDays(-3),
            UpdatedDate = nowUtc.AddDays(-2),
            Data = Unit.Value,
            DataType = typeof(Unit),
        })).ShouldBeTrue();

        var result = await maintenance.ArchiveOccurrencesAsync(new JobArchiveOccurrencesJobData
        {
            RetentionWindow = TimeSpan.FromDays(1),
            DryRun = true,
            BatchSize = 5,
        });

        result.Operation.ShouldBe("jobs-archive-occurrences");
        result.MatchedCount.ShouldBe(1);
        result.ProcessedCount.ShouldBe(0);
        result.AffectedIds.ShouldContain(occurrenceId.ToString("N"));
        result.Diagnostics.Any(x => x.Contains("would archive 1 occurrence", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
        (await store.Occurrences.GetAsync(occurrenceId)).Status.ShouldBe(JobOccurrenceStatus.Failed);
    }

    [Fact]
    public async Task ArchiveOccurrencesAsync_StatusFilter_ArchivesOnlyMatchingStatuses()
    {
        using var harness = this.CreateHarness(jobs => jobs
            .WithJob<MaintenanceTestJob>("archive-job", job => job
                .Description("Archive test job.")
                .AddTrigger("manual", trigger => trigger.Manual())));

        var store = harness.Services.GetRequiredService<IJobStoreProvider>();
        var maintenance = harness.Services.GetRequiredService<IJobSchedulerMaintenanceService>();
        var nowUtc = harness.Clock.GetUtcNow();
        var completedOccurrenceId = Guid.NewGuid();
        var failedOccurrenceId = Guid.NewGuid();

        (await store.Occurrences.TryCreateAsync(new JobOccurrence
        {
            OccurrenceId = completedOccurrenceId,
            OccurrenceKey = "archive-job:manual:completed-old",
            JobName = "archive-job",
            TriggerName = "manual",
            TriggerType = JobTriggerType.Manual,
            Status = JobOccurrenceStatus.Completed,
            DueUtc = nowUtc.AddDays(-3),
            CreatedDate = nowUtc.AddDays(-3),
            UpdatedDate = nowUtc.AddDays(-2),
            Data = Unit.Value,
            DataType = typeof(Unit),
        })).ShouldBeTrue();

        (await store.Occurrences.TryCreateAsync(new JobOccurrence
        {
            OccurrenceId = failedOccurrenceId,
            OccurrenceKey = "archive-job:manual:failed-old",
            JobName = "archive-job",
            TriggerName = "manual",
            TriggerType = JobTriggerType.Manual,
            Status = JobOccurrenceStatus.Failed,
            DueUtc = nowUtc.AddDays(-3),
            CreatedDate = nowUtc.AddDays(-3),
            UpdatedDate = nowUtc.AddDays(-2),
            Data = Unit.Value,
            DataType = typeof(Unit),
        })).ShouldBeTrue();

        var result = await maintenance.ArchiveOccurrencesAsync(new JobArchiveOccurrencesJobData
        {
            RetentionWindow = TimeSpan.FromDays(1),
            Statuses = [JobOccurrenceStatus.Failed],
            BatchSize = 5,
        });

        result.MatchedCount.ShouldBe(1);
        result.ProcessedCount.ShouldBe(1);
        (await store.Occurrences.GetAsync(completedOccurrenceId)).Status.ShouldBe(JobOccurrenceStatus.Completed);
        (await store.Occurrences.GetAsync(failedOccurrenceId)).Status.ShouldBe(JobOccurrenceStatus.Archived);
    }

    [Fact]
    public async Task PurgeOccurrencesAsync_RemovesRetainedRecordsAndRefreshesBatches()
    {
        using var harness = this.CreateHarness(jobs => jobs
            .WithJob<MaintenanceTestJob>("purge-job", job => job
                .Description("Purge test job.")
                .AddTrigger("manual", trigger => trigger.Manual()))
            .WithJob<MaintenanceTestJob>("dependent-job", job => job
                .Description("Dependent purge test job.")
                .AddTrigger("manual", trigger => trigger.Manual())));

        var store = harness.Services.GetRequiredService<IJobStoreProvider>();
        var maintenance = harness.Services.GetRequiredService<IJobSchedulerMaintenanceService>();
        var nowUtc = harness.Clock.GetUtcNow();
        var purgedOccurrenceId = Guid.NewGuid();
        var retainedOccurrenceId = Guid.NewGuid();
        var dependentOccurrenceId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        (await store.Occurrences.TryCreateAsync(new JobOccurrence
        {
            OccurrenceId = purgedOccurrenceId,
            OccurrenceKey = "purge-job:manual:archived-old",
            JobName = "purge-job",
            TriggerName = "manual",
            TriggerType = JobTriggerType.Manual,
            Status = JobOccurrenceStatus.Archived,
            DueUtc = nowUtc.AddDays(-3),
            CreatedDate = nowUtc.AddDays(-3),
            UpdatedDate = nowUtc.AddDays(-2),
            Data = Unit.Value,
            DataType = typeof(Unit),
        })).ShouldBeTrue();

        (await store.Occurrences.TryCreateAsync(new JobOccurrence
        {
            OccurrenceId = retainedOccurrenceId,
            OccurrenceKey = "purge-job:manual:completed-recent",
            JobName = "purge-job",
            TriggerName = "manual",
            TriggerType = JobTriggerType.Manual,
            Status = JobOccurrenceStatus.Completed,
            DueUtc = nowUtc.AddHours(-3),
            CreatedDate = nowUtc.AddHours(-3),
            UpdatedDate = nowUtc.AddHours(-2),
            Data = Unit.Value,
            DataType = typeof(Unit),
        })).ShouldBeTrue();

        (await store.Occurrences.TryCreateAsync(new JobOccurrence
        {
            OccurrenceId = dependentOccurrenceId,
            OccurrenceKey = "dependent-job:manual:blocked",
            JobName = "dependent-job",
            TriggerName = "manual",
            TriggerType = JobTriggerType.Manual,
            Status = JobOccurrenceStatus.Blocked,
            DueUtc = nowUtc.AddHours(-1),
            CreatedDate = nowUtc.AddHours(-1),
            UpdatedDate = nowUtc.AddHours(-1),
            Data = Unit.Value,
            DataType = typeof(Unit),
        })).ShouldBeTrue();

        await store.Executions.CreateAsync(new JobExecution
        {
            ExecutionId = Guid.NewGuid(),
            OccurrenceId = purgedOccurrenceId,
            JobName = "purge-job",
            TriggerName = "manual",
            AttemptNumber = 1,
            Status = JobExecutionStatus.Completed,
            SchedulerInstanceId = "tests",
            StartedUtc = nowUtc.AddDays(-3),
            CompletedUtc = nowUtc.AddDays(-2),
            CreatedDate = nowUtc.AddDays(-3),
            UpdatedDate = nowUtc.AddDays(-2),
        });

        await store.ExecutionHistory.AppendAsync(new JobExecutionHistoryEntry
        {
            HistoryId = Guid.NewGuid(),
            OccurrenceId = purgedOccurrenceId,
            JobName = "purge-job",
            TriggerName = "manual",
            SchedulerInstanceId = "tests",
            EventName = "OccurrenceArchived",
            OccurrenceStatus = JobOccurrenceStatus.Archived,
            RecordedAt = nowUtc.AddDays(-2),
        });

        await store.Dependencies.AddAsync(new JobOccurrenceDependency
        {
            DependencyId = Guid.NewGuid(),
            DependentOccurrenceId = dependentOccurrenceId,
            PrerequisiteOccurrenceId = purgedOccurrenceId,
            RequiredStatuses = [JobOccurrenceStatus.Completed],
            CreatedDate = nowUtc.AddDays(-2),
            UpdatedDate = nowUtc.AddDays(-2),
        });

        await store.Leases.UpsertAsync(new JobLeaseRecord
        {
            OccurrenceId = purgedOccurrenceId,
            SchedulerInstanceId = "tests",
            OwnershipToken = "lease-token",
            AcquiredUtc = nowUtc.AddDays(-2),
            ExpiresUtc = nowUtc.AddDays(1),
            CreatedDate = nowUtc.AddDays(-2),
            UpdatedDate = nowUtc.AddDays(-2),
        });

        (await store.Batches.TryCreateAsync(
            new JobBatch
            {
                BatchId = batchId,
                ExternalBatchId = "purge-batch",
                Description = "Purge batch.",
                Status = JobBatchStatus.CompletedWithFailures,
                CompletionPolicy = JobBatchCompletionPolicy.AllowPartialCompletion,
                AcceptedCount = 2,
                SucceededCount = 1,
                ArchivedCount = 1,
                CreatedDate = nowUtc.AddDays(-3),
                UpdatedDate = nowUtc.AddDays(-2),
                CompletedDate = nowUtc.AddDays(-2),
            },
            [
                new JobBatchOccurrence
                {
                    BatchId = batchId,
                    OccurrenceId = purgedOccurrenceId,
                    ChildStatus = JobOccurrenceStatus.Archived,
                    Sequence = 1,
                    CreatedDate = nowUtc.AddDays(-3),
                    UpdatedDate = nowUtc.AddDays(-2),
                },
                new JobBatchOccurrence
                {
                    BatchId = batchId,
                    OccurrenceId = retainedOccurrenceId,
                    ChildStatus = JobOccurrenceStatus.Completed,
                    Sequence = 2,
                    CreatedDate = nowUtc.AddDays(-3),
                    UpdatedDate = nowUtc.AddHours(-2),
                },
            ])).ShouldBeTrue();

        var result = await maintenance.PurgeOccurrencesAsync(new JobPurgeOccurrencesRequest
        {
            OlderThan = nowUtc.AddDays(-1),
            Statuses = [JobOccurrenceStatus.Archived],
            JobName = "purge-job",
            IsArchived = true,
        });

        result.Operation.ShouldBe("jobs-purge-occurrences");
        result.MatchedCount.ShouldBe(1);
        result.ProcessedCount.ShouldBe(1);
        (await store.Occurrences.GetAsync(purgedOccurrenceId)).ShouldBeNull();
        (await store.Occurrences.GetAsync(retainedOccurrenceId)).ShouldNotBeNull();
        (await store.Executions.ListByOccurrenceAsync(purgedOccurrenceId)).ShouldBeEmpty();
        (await store.ExecutionHistory.ListAsync(purgedOccurrenceId)).ShouldBeEmpty();
        (await store.Dependencies.ListByPrerequisiteAsync(purgedOccurrenceId)).ShouldBeEmpty();
        (await store.Dependencies.ListByDependentAsync(dependentOccurrenceId)).ShouldBeEmpty();
        (await store.Leases.GetAsync(purgedOccurrenceId)).ShouldBeNull();

        var batch = await store.Batches.GetAsync(batchId);
        var memberships = await store.Batches.ListOccurrencesAsync(batchId);

        batch.AcceptedCount.ShouldBe(1);
        batch.SucceededCount.ShouldBe(1);
        batch.ArchivedCount.ShouldBe(0);
        batch.Status.ShouldBe(JobBatchStatus.Completed);
        memberships.Count.ShouldBe(1);
        memberships.Single().OccurrenceId.ShouldBe(retainedOccurrenceId);
    }

    [Fact]
    public async Task PurgeOccurrencesAsync_DryRun_LeavesDataUntouched()
    {
        using var harness = this.CreateHarness(jobs => jobs
            .WithJob<MaintenanceTestJob>("purge-job", job => job
                .Description("Purge test job.")
                .AddTrigger("manual", trigger => trigger.Manual())));

        var store = harness.Services.GetRequiredService<IJobStoreProvider>();
        var maintenance = harness.Services.GetRequiredService<IJobSchedulerMaintenanceService>();
        var nowUtc = harness.Clock.GetUtcNow();
        var occurrenceId = Guid.NewGuid();

        (await store.Occurrences.TryCreateAsync(new JobOccurrence
        {
            OccurrenceId = occurrenceId,
            OccurrenceKey = "purge-job:manual:dry-run",
            JobName = "purge-job",
            TriggerName = "manual",
            TriggerType = JobTriggerType.Manual,
            Status = JobOccurrenceStatus.Archived,
            DueUtc = nowUtc.AddDays(-2),
            CreatedDate = nowUtc.AddDays(-2),
            UpdatedDate = nowUtc.AddDays(-2),
            Data = Unit.Value,
            DataType = typeof(Unit),
        })).ShouldBeTrue();

        var result = await maintenance.PurgeOccurrencesAsync(new JobPurgeOccurrencesRequest
        {
            OlderThan = nowUtc.AddDays(-1),
            DryRun = true,
        });

        result.ProcessedCount.ShouldBe(0);
        result.MatchedCount.ShouldBe(1);
        result.AffectedIds.ShouldContain(occurrenceId.ToString("N"));
        (await store.Occurrences.GetAsync(occurrenceId)).ShouldNotBeNull();
    }

    [Fact]
    public async Task PurgeOccurrencesAsync_NoMatches_ReturnsZeroCounts()
    {
        using var harness = this.CreateHarness(jobs => jobs
            .WithJob<MaintenanceTestJob>("purge-job", job => job
                .Description("Purge test job.")
                .AddTrigger("manual", trigger => trigger.Manual())));

        var maintenance = harness.Services.GetRequiredService<IJobSchedulerMaintenanceService>();

        var result = await maintenance.PurgeOccurrencesAsync(new JobPurgeOccurrencesRequest
        {
            OlderThan = harness.Clock.GetUtcNow().AddDays(-1),
            JobName = "missing-job",
            Statuses = [JobOccurrenceStatus.Archived],
        });

        result.MatchedCount.ShouldBe(0);
        result.ProcessedCount.ShouldBe(0);
        result.AffectedIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task PurgeOccurrencesAsync_StatusFilter_PreservesNonMatchingActiveOccurrences()
    {
        using var harness = this.CreateHarness(jobs => jobs
            .WithJob<MaintenanceTestJob>("purge-job", job => job
                .Description("Purge test job.")
                .AddTrigger("manual", trigger => trigger.Manual())));

        var store = harness.Services.GetRequiredService<IJobStoreProvider>();
        var maintenance = harness.Services.GetRequiredService<IJobSchedulerMaintenanceService>();
        var nowUtc = harness.Clock.GetUtcNow();
        var occurrenceId = Guid.NewGuid();

        (await store.Occurrences.TryCreateAsync(new JobOccurrence
        {
            OccurrenceId = occurrenceId,
            OccurrenceKey = "purge-job:manual:running-old",
            JobName = "purge-job",
            TriggerName = "manual",
            TriggerType = JobTriggerType.Manual,
            Status = JobOccurrenceStatus.Running,
            DueUtc = nowUtc.AddDays(-3),
            CreatedDate = nowUtc.AddDays(-3),
            UpdatedDate = nowUtc.AddDays(-2),
            Data = Unit.Value,
            DataType = typeof(Unit),
        })).ShouldBeTrue();

        var result = await maintenance.PurgeOccurrencesAsync(new JobPurgeOccurrencesRequest
        {
            OlderThan = nowUtc.AddDays(-1),
            JobName = "purge-job",
            Statuses = [JobOccurrenceStatus.Archived, JobOccurrenceStatus.Completed],
        });

        result.MatchedCount.ShouldBe(0);
        result.ProcessedCount.ShouldBe(0);
        (await store.Occurrences.GetAsync(occurrenceId)).ShouldNotBeNull();
    }

    private sealed class MaintenanceTestJob : JobBase
    {
        public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());
    }
}
