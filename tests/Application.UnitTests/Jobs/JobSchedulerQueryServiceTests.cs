// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Jobs;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public class JobSchedulerQueryServiceTests(ITestOutputHelper output) : JobSchedulerTestBase(output)
{
    [Fact]
    public async Task QueryJobsAsync_OverlaysPersistedRuntimeState()
    {
        using var harness = this.CreateHarness(jobs =>
            jobs.WithJob<NoOpQueryJob>("overlay-job", job => job
                .Description("overlay")
                .AddTrigger("manual", trigger => trigger.Manual())));
        var sut = harness.Services.GetRequiredService<IJobSchedulerQueryService>();

        (await harness.Scheduler.PauseJobAsync("overlay-job")).IsSuccess.ShouldBeTrue();

        var result = await sut.QueryJobsAsync(new JobSchedulerJobQueryRequest { JobName = "overlay-job" });

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(1);
        result.Value.Single().JobName.ShouldBe("overlay-job");
        result.Value.Single().Paused.ShouldBeTrue();
        result.Value.Single().EffectiveEnabled.ShouldBeTrue();
        result.Value.Single().TriggerCount.ShouldBe(1);
    }

    [Fact]
    public async Task QueryTriggersAndRecurringTriggersAsync_ReturnRegisteredAndRuntimeState()
    {
        using var harness = this.CreateHarness(jobs =>
            jobs.WithJob<NoOpQueryJob>("multi-job", job => job
                .Description("multi")
                .AddTrigger("manual", trigger => trigger.Manual())
                .AddTrigger("cron", trigger => trigger.Cron("* * * * *"))));
        var sut = harness.Services.GetRequiredService<IJobSchedulerQueryService>();

        (await harness.Scheduler.PauseTriggerAsync("multi-job", "manual")).IsSuccess.ShouldBeTrue();
        harness.Advance(TimeSpan.FromMinutes(1));
        (await harness.MaterializeAsync()).IsSuccess.ShouldBeTrue();

        var triggerResult = await sut.QueryTriggersAsync(new JobSchedulerTriggerQueryRequest { JobName = "multi-job" });
        var recurringResult = await sut.QueryRecurringTriggersAsync(new JobSchedulerRecurringTriggerQueryRequest { JobName = "multi-job" });

        triggerResult.IsSuccess.ShouldBeTrue();
        triggerResult.TotalCount.ShouldBe(2);
        triggerResult.Value.Single(x => x.TriggerName == "manual").Paused.ShouldBeTrue();
        recurringResult.IsSuccess.ShouldBeTrue();
        recurringResult.TotalCount.ShouldBe(1);
        recurringResult.Value.Single().TriggerName.ShouldBe("cron");
        recurringResult.Value.Single().TriggerType.ShouldBe(JobTriggerType.Cron);
        recurringResult.Value.Single().Schedule.ShouldBe("* * * * *");
    }

    [Fact]
    public async Task QueryJobsAsync_UsesResolvedModuleFromAccessor()
    {
        using var harness = this.CreateHarness(
            jobs => jobs.WithJob<NoOpQueryJob>("module-job", job => job
                .Description("module")
                .AddTrigger("manual", trigger => trigger.Manual())),
            services => services.AddSingleton<IModuleContextAccessor>(new StaticModuleContextAccessor(typeof(NoOpQueryJob), new TestModule("Reporting"))));
        var sut = harness.Services.GetRequiredService<IJobSchedulerQueryService>();

        var result = await sut.QueryJobsAsync(new JobSchedulerJobQueryRequest { JobName = "module-job" });

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(1);
        result.Value.Single().Module.ShouldBe("Reporting");
        result.Value.Single().DisplayName.ShouldBe("reporting-no-op-query-job");
    }

    [Fact]
    public async Task QueryTriggersAsync_ExposesEffectiveTargetInstances()
    {
        using var harness = this.CreateHarness(jobs =>
            jobs.WithJob<NoOpQueryJob>("target-job", job => job
                .Description("targets")
                .TargetInstances("node-a")
                .AddTrigger("manual", trigger => trigger.Manual())
                .AddTrigger("cron", trigger => trigger.Cron("* * * * *").TargetInstances("node-b"))));
        var sut = harness.Services.GetRequiredService<IJobSchedulerQueryService>();

        var result = await sut.QueryTriggersAsync(new JobSchedulerTriggerQueryRequest { JobName = "target-job" });

        result.IsSuccess.ShouldBeTrue();
        result.Value.Single(x => x.TriggerName == "manual").TargetInstances.ShouldBe(["node-a"]);
        result.Value.Single(x => x.TriggerName == "cron").TargetInstances.ShouldBe(["node-b"]);
    }

    [Fact]
    public async Task QueryOccurrencesAsync_FiltersByOperationalFields_AndRedactsPayloads()
    {
        using var harness = this.CreateHarness(jobs =>
            jobs.WithJob<SecretPayloadJob>("secret-job", job => job
                .Description("secret")
                .AddTrigger("manual", trigger => trigger.Manual())));
        var sut = harness.Services.GetRequiredService<IJobSchedulerQueryService>();
        var dispatch = await harness.DispatchAndWaitAsync<SecretPayloadJob>(
            new SecretPayload("s3cr3t-token"),
            new JobDispatchOptions
            {
                CorrelationId = "corr-42",
                IdempotencyKey = "idem-42",
                Properties = new PropertyBag { ["secret"] = "top-secret" },
            });

        dispatch.IsSuccess.ShouldBeTrue();
        var occurrence = await harness.GetOccurrenceAsync(dispatch.Value.OccurrenceId);

        var result = await sut.QueryOccurrencesAsync(new JobSchedulerOccurrenceQueryRequest
        {
            JobName = "secret-job",
            TriggerName = "manual",
            CorrelationId = occurrence.CorrelationId,
            IdempotencyKey = occurrence.IdempotencyKey,
            Statuses = [occurrence.Status],
            CreatedFromUtc = occurrence.CreatedDate.AddMinutes(-1),
            CreatedToUtc = occurrence.CreatedDate.AddMinutes(1),
        });

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(1);
        result.Value.Single().OccurrenceId.ShouldBe(dispatch.Value.OccurrenceId);
        result.Value.Single().DataPreview.ShouldBe(nameof(SecretPayload));
        result.Value.Single().DataPreview.ShouldNotContain("s3cr3t-token");
        result.Value.Single().PropertyKeys.ShouldContain("secret");
    }

    [Fact]
    public async Task QueryRetriesAsync_ReturnsPendingRetryView()
    {
        RetryQueryJob.Reset(failuresBeforeSuccess: 1);
        using var harness = this.CreateHarness(jobs =>
            jobs.WithJob<RetryQueryJob>("retry-job", job => job
                .Description("retry")
                .WithRetry(retry => retry.MaxAttempts(2).FixedDelay(TimeSpan.FromMinutes(5)))
                .AddTrigger("manual", trigger => trigger.Manual())));
        var sut = harness.Services.GetRequiredService<IJobSchedulerQueryService>();

        var dispatch = await harness.DispatchAsync<RetryQueryJob>();
        dispatch.IsSuccess.ShouldBeTrue();
        var failedAttempt = await harness.ExecuteOccurrenceAsync(dispatch.Value.OccurrenceId);
        failedAttempt.IsSuccess.ShouldBeTrue();
        failedAttempt.Value.Status.ShouldBe(JobExecutionStatus.Retried);

        var result = await sut.QueryRetriesAsync(new JobSchedulerRetryQueryRequest { JobName = "retry-job" });

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(1);
        result.Value.Single().OccurrenceId.ShouldBe(dispatch.Value.OccurrenceId);
        result.Value.Single().AttemptCount.ShouldBe(1);
        result.Value.Single().MaxAttempts.ShouldBe(2);
        result.Value.Single().HasRemainingAttempts.ShouldBeTrue();
        result.Value.Single().NextAttemptNumber.ShouldBe(2);
    }

    [Fact]
    public async Task QueryBatchesAndBatchOccurrencesAsync_ReturnBatchSummariesAndChildren()
    {
        using var harness = this.CreateHarness(jobs =>
        {
            jobs.WithJob<NoOpQueryJob>("success-job", job => job.Description("success").AddTrigger("manual", trigger => trigger.Manual()));
            jobs.WithJob<AlwaysFailQueryJob>("fail-job", job => job.Description("fail").AddTrigger("manual", trigger => trigger.Manual()));
        });
        var sut = harness.Services.GetRequiredService<IJobSchedulerQueryService>();

        var dispatch = await harness.Scheduler.DispatchBatchAsync(new JobBatchDispatchRequest
        {
            BatchId = "batch-query",
            CompletionPolicy = JobBatchCompletionPolicy.AllowPartialCompletion,
            Items =
            [
                new JobBatchDispatchItem { JobName = "success-job", Data = Unit.Value },
                new JobBatchDispatchItem { JobName = "fail-job", Data = Unit.Value },
            ],
        });

        dispatch.IsSuccess.ShouldBeTrue();
        foreach (var occurrenceId in dispatch.Value.OccurrenceIds)
        {
            await harness.ExecuteOccurrenceAsync(occurrenceId);
        }

        var batchResult = await sut.QueryBatchesAsync(new JobSchedulerBatchQueryRequest { BatchId = "batch-query" });
        var childResult = await sut.QueryBatchOccurrencesAsync("batch-query");

        batchResult.IsSuccess.ShouldBeTrue();
        batchResult.TotalCount.ShouldBe(1);
        batchResult.Value.Single().Status.ShouldBe(JobBatchStatus.CompletedWithFailures);
        batchResult.Value.Single().ChildOccurrenceCount.ShouldBe(2);
        childResult.IsSuccess.ShouldBeTrue();
        childResult.TotalCount.ShouldBe(2);
        childResult.Value.ShouldContain(x => x.JobName == "success-job" && x.ChildStatus == JobOccurrenceStatus.Completed);
        childResult.Value.ShouldContain(x => x.JobName == "fail-job" && x.ChildStatus == JobOccurrenceStatus.Failed);
    }

    [Fact]
    public async Task QueryBatchHistoryAsync_ReturnsPersistedBatchAuditEvents()
    {
        using var harness = this.CreateHarness(jobs =>
            jobs.WithJob<NoOpQueryJob>("success-job", job => job.Description("success").AddTrigger("manual", trigger => trigger.Manual())));
        var sut = harness.Services.GetRequiredService<IJobSchedulerQueryService>();

        var dispatch = await harness.Scheduler.DispatchBatchAsync(new JobBatchDispatchRequest
        {
            BatchId = "batch-history-query",
            Items = [new JobBatchDispatchItem { JobName = "success-job", Data = Unit.Value }],
        });

        dispatch.IsSuccess.ShouldBeTrue();
        await harness.ExecuteOccurrenceAsync(dispatch.Value.OccurrenceIds.Single());

        var result = await sut.QueryBatchHistoryAsync("batch-history-query", new JobSchedulerBatchHistoryQueryRequest
        {
            SortDescending = false,
        });

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBeGreaterThanOrEqualTo(2);
        result.Value.ShouldContain(x => x.EventName == "BatchCreated");
        result.Value.ShouldContain(x => x.EventName == "BatchDispatched");
        result.Value.ShouldContain(x => x.ExternalBatchId == "batch-history-query");
    }

    [Fact]
    public async Task QueryDependenciesAsync_ReturnsDependencyStateAndOccurrenceDetails()
    {
        using var harness = this.CreateHarness(jobs =>
        {
            jobs.WithJob<NoOpQueryJob>("predecessor", job => job
                .Description("predecessor")
                .AddTrigger("once", trigger => trigger.At(new DateTimeOffset(2026, 05, 26, 09, 01, 00, TimeSpan.Zero)))
                .Then("successor", chain => chain.WithTrigger("manual")));
            jobs.WithJob<NoOpQueryJob>("successor", job => job.Description("successor").AddTrigger("manual", trigger => trigger.Manual()));
        });
        var sut = harness.Services.GetRequiredService<IJobSchedulerQueryService>();
        var store = harness.Services.GetRequiredService<IJobStoreProvider>();

        harness.Advance(TimeSpan.FromMinutes(1));
        var materialized = await harness.MaterializeAsync();
        materialized.IsSuccess.ShouldBeTrue();
        var successor = (await store.Queries.ListOccurrencesAsync()).Single(x => x.JobName == "successor");

        var dependencies = await sut.QueryDependenciesAsync(new JobSchedulerDependencyQueryRequest { OccurrenceId = successor.OccurrenceId });
        var occurrences = await sut.QueryOccurrencesAsync(new JobSchedulerOccurrenceQueryRequest { OccurrenceId = successor.OccurrenceId });

        dependencies.IsSuccess.ShouldBeTrue();
        dependencies.TotalCount.ShouldBe(1);
        dependencies.Value.Single().DependentJobName.ShouldBe("successor");
        dependencies.Value.Single().PrerequisiteJobName.ShouldBe("predecessor");
        dependencies.Value.Single().Status.ShouldBe(JobDependencyStatus.Pending);
        occurrences.IsSuccess.ShouldBeTrue();
        occurrences.Value.Single().DependencyCount.ShouldBe(1);
        occurrences.Value.Single().PendingDependencyCount.ShouldBe(1);
    }

    [Fact]
    public async Task QueryExecutionsAndHistoryAsync_ReturnExecutionAttemptsAndLifecycleHistory()
    {
        RetryQueryJob.Reset(failuresBeforeSuccess: 1);
        using var harness = this.CreateHarness(jobs =>
            jobs.WithJob<RetryQueryJob>("retry-job", job => job
                .Description("retry")
                .WithRetry(retry => retry.MaxAttempts(2).FixedDelay(TimeSpan.FromMinutes(1)))
                .AddTrigger("manual", trigger => trigger.Manual())));
        var sut = harness.Services.GetRequiredService<IJobSchedulerQueryService>();

        var dispatch = await harness.DispatchAsync<RetryQueryJob>();
        var failedAttempt = await harness.ExecuteOccurrenceAsync(dispatch.Value.OccurrenceId);
        failedAttempt.IsSuccess.ShouldBeTrue();
        failedAttempt.Value.Status.ShouldBe(JobExecutionStatus.Retried);
        harness.Advance(TimeSpan.FromMinutes(1));
        await harness.SweepAsync();

        var executionResult = await sut.QueryExecutionsAsync(new JobSchedulerExecutionQueryRequest { JobName = "retry-job" });
        var historyResult = await sut.QueryExecutionHistoryAsync(new JobSchedulerExecutionHistoryQueryRequest { OccurrenceId = dispatch.Value.OccurrenceId });

        executionResult.IsSuccess.ShouldBeTrue();
        executionResult.TotalCount.ShouldBe(2);
        executionResult.Value.Select(x => x.AttemptNumber).OrderBy(x => x).ShouldBe([1, 2]);
        historyResult.IsSuccess.ShouldBeTrue();
        historyResult.Value.ShouldContain(x => x.EventName == "ExecutionStarted");
        historyResult.Value.ShouldContain(x => x.EventName == "ExecutionRetried");
        historyResult.Value.ShouldContain(x => x.EventName == "ExecutionCompleted");
    }

    [Fact]
    public async Task QueryLeasesAndServersAsync_ReturnActiveDiagnostics()
    {
        LeaseHoldingJob.Reset();
        using var harness = this.CreateHarness(jobs =>
            jobs.WithJob<LeaseHoldingJob>("lease-job", job => job
                .Description("lease")
                .AddTrigger("manual", trigger => trigger.Manual())));
        var sut = harness.Services.GetRequiredService<IJobSchedulerQueryService>();

        var executionTask = harness.DispatchAndWaitAsync<LeaseHoldingJob>();
        await LeaseHoldingJob.Started.Task;

        var leaseResult = await sut.QueryLeasesAsync(new JobSchedulerLeaseQueryRequest { JobName = "lease-job" });
        var serverResult = await sut.QueryServersAsync();

        leaseResult.IsSuccess.ShouldBeTrue();
        leaseResult.TotalCount.ShouldBe(1);
        leaseResult.Value.Single().Status.ShouldBe(JobSchedulerLeaseStatus.Active);
        serverResult.IsSuccess.ShouldBeTrue();
        serverResult.Value.ShouldContain(x => x.Status == JobSchedulerServerStatus.Active && !string.IsNullOrWhiteSpace(x.SchedulerInstanceId));

        LeaseHoldingJob.ReleaseExecution();
        (await executionTask).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task GetMetricsAndDashboardQueries_UsePersistedState()
    {
        RetryQueryJob.Reset(failuresBeforeSuccess: 1);
        LeaseHoldingJob.Reset();
        using var harness = this.CreateHarness(jobs =>
        {
            jobs.WithJob<NoOpQueryJob>("success-job", job => job.Description("success").AddTrigger("manual", trigger => trigger.Manual()));
            jobs.WithJob<RetryQueryJob>("retry-job", job => job.Description("retry").WithRetry(retry => retry.MaxAttempts(2).FixedDelay(TimeSpan.FromMinutes(5))).AddTrigger("manual", trigger => trigger.Manual()));
            jobs.WithJob<LeaseHoldingJob>("lease-job", job => job.Description("lease").AddTrigger("manual", trigger => trigger.Manual()));
        });
        var sut = harness.Services.GetRequiredService<IJobSchedulerQueryService>();

        (await harness.DispatchAndWaitAsync<NoOpQueryJob>()).IsSuccess.ShouldBeTrue();
        var retryDispatch = await harness.DispatchAsync<RetryQueryJob>();
        retryDispatch.IsSuccess.ShouldBeTrue();
        var failedAttempt = await harness.ExecuteOccurrenceAsync(retryDispatch.Value.OccurrenceId);
        failedAttempt.IsSuccess.ShouldBeTrue();
        failedAttempt.Value.Status.ShouldBe(JobExecutionStatus.Retried);
        var executionTask = harness.DispatchAndWaitAsync<LeaseHoldingJob>();
        await LeaseHoldingJob.Started.Task;

        var metrics = await sut.GetMetricsAsync();
        var summary = await sut.GetDashboardSummaryAsync();
        var timeline = await sut.GetDashboardTimelineAsync(new JobSchedulerTimelineRequest
        {
            Mode = JobSchedulerTimelineMode.Executions,
            FromUtc = harness.Clock.GetUtcNow().AddHours(-1),
            ToUtc = harness.Clock.GetUtcNow().AddHours(1),
            BucketMinutes = 60,
        });

        metrics.IsSuccess.ShouldBeTrue();
        metrics.Value.OccurrenceCount.ShouldBeGreaterThanOrEqualTo(3);
        metrics.Value.ExecutionCount.ShouldBeGreaterThanOrEqualTo(2);
        metrics.Value.RetryScheduledCount.ShouldBe(1);
        metrics.Value.ActiveLeaseCount.ShouldBeGreaterThanOrEqualTo(1);
        summary.IsSuccess.ShouldBeTrue();
        summary.Value.RunningOccurrenceCount.ShouldBeGreaterThanOrEqualTo(1);
        summary.Value.RetryScheduledCount.ShouldBe(1);
        timeline.IsSuccess.ShouldBeTrue();
        timeline.Value.Buckets.ShouldNotBeEmpty();
        timeline.Value.Buckets.Any(x => x.CountsByStatus.Count > 0).ShouldBeTrue();

        LeaseHoldingJob.ReleaseExecution();
        (await executionTask).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task QueryJobsAsync_PagingAndSorting_AreDeterministic()
    {
        using var harness = this.CreateHarness(jobs =>
        {
            jobs.WithJob<NoOpQueryJob>("gamma", job => job.Description("g").AddTrigger("manual", trigger => trigger.Manual()));
            jobs.WithJob<NoOpQueryJob>("alpha", job => job.Description("a").AddTrigger("manual", trigger => trigger.Manual()));
            jobs.WithJob<NoOpQueryJob>("beta", job => job.Description("b").AddTrigger("manual", trigger => trigger.Manual()));
        });
        var sut = harness.Services.GetRequiredService<IJobSchedulerQueryService>();

        var result = await sut.QueryJobsAsync(new JobSchedulerJobQueryRequest
        {
            SortBy = "Group",
            SortDescending = false,
            Skip = 1,
            Take = 1,
        });

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(3);
        result.Value.Single().JobName.ShouldBe("beta");
    }

    [Fact]
    public async Task QueryJobsAndDashboardAsync_IncludeOrphanedRuntimeState_ExposeSyntheticRowsAndFacetCounts()
    {
        using var harness = this.CreateHarness(jobs =>
            jobs.WithJob<NoOpQueryJob>("known-job", job => job
                .Description("known")
                .AddTrigger("manual", trigger => trigger.Manual())));
        var sut = harness.Services.GetRequiredService<IJobSchedulerQueryService>();
        var stores = harness.Services.GetRequiredService<IJobStoreProvider>();
        var nowUtc = harness.Clock.GetUtcNow();

        await stores.RuntimeStates.UpsertAsync(new JobRuntimeState
        {
            JobName = "orphan-job",
            Enabled = false,
            Paused = true,
            CreatedDate = nowUtc,
            UpdatedDate = nowUtc,
        });

        await stores.TriggerRuntimeStates.UpsertAsync(
            "known-job",
            "removed-trigger",
            new JobTriggerRuntimeState(
                ActivatedUtc: null,
                DueUtc: nowUtc,
                LastMaterializedScheduledUtc: null,
                HasMaterializedOccurrence: false,
                Enabled: false,
                Paused: true,
                CreatedDate: nowUtc,
                UpdatedDate: nowUtc));

        var jobs = await sut.QueryJobsAsync(new JobSchedulerJobQueryRequest
        {
            IncludeOrphanedRuntimeState = true,
            SortDescending = false,
        });
        var navigation = await sut.GetDashboardNavigationAsync();
        var overview = await sut.GetDashboardOverviewAsync();

        jobs.IsSuccess.ShouldBeTrue();
        jobs.Value.ShouldContain(x => x.JobName == "orphan-job" && x.IsOrphanedRuntimeState && x.Paused && !x.EffectiveEnabled);
        jobs.Value.Single(x => x.JobName == "known-job").HasOrphanedRuntimeState.ShouldBeTrue();
        navigation.IsSuccess.ShouldBeTrue();
        navigation.Value.JobFacets.OrphanedRuntimeStateCount.ShouldBeGreaterThanOrEqualTo(2);
        navigation.Value.Links.ShouldContain(x => x.Key == "orphaned-runtime-state" && x.Count >= 2);
        overview.IsSuccess.ShouldBeTrue();
        overview.Value.JobFacets.OrphanedRuntimeStateCount.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task QueryOccurrencesAndExecutionsAsync_SupportSpecShapedOperationalFilters()
    {
        using var harness = this.CreateHarness(jobs =>
            jobs.WithJob<NoOpQueryJob>("filter-job", job => job
                .Description("filters")
                .AddTrigger("manual", trigger => trigger.Manual())));
        var sut = harness.Services.GetRequiredService<IJobSchedulerQueryService>();

        var dispatch = await harness.DispatchAndWaitAsync<NoOpQueryJob>(
            cancellationToken: CancellationToken.None);
        var occurrence = await harness.GetOccurrenceAsync(dispatch.Value.OccurrenceId);
        var execution = (await harness.GetExecutionsAsync(dispatch.Value.OccurrenceId)).Single();

        var occurrenceResult = await sut.QueryOccurrencesAsync(new JobSchedulerOccurrenceQueryRequest
        {
            JobName = "filter-job",
            TriggerName = "manual",
            TriggerType = JobTriggerType.Manual,
            Statuses = [JobOccurrenceStatus.Completed],
            DueFrom = occurrence.DueUtc.AddMinutes(-1),
            DueTo = occurrence.DueUtc.AddMinutes(1),
            StartedFrom = execution.StartedUtc.AddMinutes(-1),
            StartedTo = execution.StartedUtc.AddMinutes(1),
            CompletedFrom = execution.CompletedUtc!.Value.AddMinutes(-1),
            CompletedTo = execution.CompletedUtc!.Value.AddMinutes(1),
        });

        var executionResult = await sut.QueryExecutionsAsync(new JobSchedulerExecutionQueryRequest
        {
            JobName = "filter-job",
            TriggerName = "manual",
            TriggerType = JobTriggerType.Manual,
            Statuses = [JobExecutionStatus.Completed],
            DueFrom = occurrence.DueUtc.AddMinutes(-1),
            DueTo = occurrence.DueUtc.AddMinutes(1),
            StartedFrom = execution.StartedUtc.AddMinutes(-1),
            StartedTo = execution.StartedUtc.AddMinutes(1),
            CompletedFrom = execution.CompletedUtc!.Value.AddMinutes(-1),
            CompletedTo = execution.CompletedUtc!.Value.AddMinutes(1),
        });

        occurrenceResult.IsSuccess.ShouldBeTrue();
        occurrenceResult.TotalCount.ShouldBe(1);
        occurrenceResult.Value.Single().OccurrenceId.ShouldBe(dispatch.Value.OccurrenceId);
        executionResult.IsSuccess.ShouldBeTrue();
        executionResult.TotalCount.ShouldBe(1);
        executionResult.Value.Single().ExecutionId.ShouldBe(execution.ExecutionId);
    }

    [Fact]
    public async Task QueryOccurrencesAsync_UnmatchedFilters_ReturnEmptySuccessfulResult()
    {
        using var harness = this.CreateHarness(jobs =>
            jobs.WithJob<NoOpQueryJob>("known-job", job => job
                .Description("known")
                .AddTrigger("manual", trigger => trigger.Manual())));
        var sut = harness.Services.GetRequiredService<IJobSchedulerQueryService>();

        (await harness.DispatchAndWaitAsync<NoOpQueryJob>()).IsSuccess.ShouldBeTrue();

        var result = await sut.QueryOccurrencesAsync(new JobSchedulerOccurrenceQueryRequest
        {
            JobName = "missing-job",
            CorrelationId = "missing-correlation",
        });

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(0);
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task QueryBatchOccurrencesAsync_UnknownBatch_ReturnsFailure()
    {
        using var harness = this.CreateHarness(jobs =>
            jobs.WithJob<NoOpQueryJob>("known-job", job => job
                .Description("known")
                .AddTrigger("manual", trigger => trigger.Manual())));
        var sut = harness.Services.GetRequiredService<IJobSchedulerQueryService>();

        var result = await sut.QueryBatchOccurrencesAsync("missing-batch");

        result.IsFailure.ShouldBeTrue();
    }

    private sealed record SecretPayload(string Secret);

    private sealed class NoOpQueryJob : JobBase<Unit>
    {
        public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());
    }

    private sealed class SecretPayloadJob : JobBase<SecretPayload>
    {
        public override Task<Result> ExecuteAsync(IJobExecutionContext<SecretPayload> context, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());
    }

    private sealed class AlwaysFailQueryJob : JobBase<Unit>
    {
        public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Failure().WithError(new ValidationError("failed")));
    }

    private sealed class RetryQueryJob : JobBase<Unit>
    {
        private static int failuresRemaining;

        public static void Reset(int failuresBeforeSuccess)
        {
            failuresRemaining = failuresBeforeSuccess;
        }

        public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Decrement(ref failuresRemaining) >= 0)
            {
                return Task.FromResult(Result.Failure().WithError(new ValidationError("retry")));
            }

            return Task.FromResult(Result.Success());
        }
    }

    private sealed class LeaseHoldingJob : JobBase<Unit>
    {
        private static TaskCompletionSource started = CreateCompletionSource();
        private static TaskCompletionSource released = CreateCompletionSource();

        public static TaskCompletionSource Started => started;

        public static void Reset()
        {
            started = CreateCompletionSource();
            released = CreateCompletionSource();
        }

        public static void ReleaseExecution()
        {
            released.TrySetResult();
        }

        public override async Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
        {
            started.TrySetResult();
            await released.Task.WaitAsync(cancellationToken);

            return Result.Success();
        }

        private static TaskCompletionSource CreateCompletionSource()
            => new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class StaticModuleContextAccessor(Type jobType, IModule module) : IModuleContextAccessor
    {
        public IModule Find(Type type) => type == jobType ? module : null;
    }

    private sealed class TestModule(string name) : IModule
    {
        public bool Enabled { get; set; } = true;

        public bool IsRegistered { get; set; } = true;

        public string Name { get; } = name;

        public int Priority => 0;

        public IServiceCollection Register(IServiceCollection services, IConfiguration configuration = null, IWebHostEnvironment environment = null) => services;

        public IApplicationBuilder Use(IApplicationBuilder app, IConfiguration configuration = null, IWebHostEnvironment environment = null) => app;
    }
}
