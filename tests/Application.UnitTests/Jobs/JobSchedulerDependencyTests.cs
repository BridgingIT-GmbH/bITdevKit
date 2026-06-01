// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Jobs;

using System.Threading;
using BridgingIT.DevKit.Application.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

public class JobSchedulerDependencyTests(ITestOutputHelper output) : JobSchedulerTestBase(output)
{
    [Fact]
    public async Task MaterializeScheduledOccurrencesAsync_ChainedSuccessorMaterializesAsNormalBlockedOccurrence()
    {
        var dueUtc = new DateTimeOffset(2026, 05, 26, 09, 01, 00, TimeSpan.Zero);
        var provider = CreateProvider(services =>
        {
            services.AddJobScheduler()
                .WithJob<SuccessfulDependencyJob>("predecessor", job => job
                    .Description("Runs first.")
                    .AddTrigger("once", trigger => trigger.At(dueUtc))
                    .Then("successor", chain => chain.WithTrigger("manual")))
                .WithJob<SuccessfulDependencyJob>("successor", job => job
                    .Description("Runs second.")
                    .AddTrigger("manual", trigger => trigger.Manual()));
        });

        var fakeTime = (FakeTimeProvider)provider.GetRequiredService<TimeProvider>();
        fakeTime.Advance(TimeSpan.FromMinutes(1));
        var scheduler = provider.GetRequiredService<JobSchedulerService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        await scheduler.MaterializeScheduledOccurrencesAsync(fakeTime.GetUtcNow(), 100);

        var occurrences = await store.Queries.ListOccurrencesAsync();
        var predecessor = occurrences.Single(x => x.JobName == "predecessor");
        var successor = occurrences.Single(x => x.JobName == "successor");
        var dependencies = await store.Dependencies.ListByDependentAsync(successor.OccurrenceId);

        predecessor.Status.ShouldBe(JobOccurrenceStatus.Due);
        successor.Status.ShouldBe(JobOccurrenceStatus.Blocked);
        successor.TriggerName.ShouldBe("manual");
        successor.BlockedReason.ShouldNotBeNullOrWhiteSpace();
        dependencies.Count.ShouldBe(1);
        dependencies[0].PrerequisiteOccurrenceId.ShouldBe(predecessor.OccurrenceId);
        dependencies[0].Status.ShouldBe(JobDependencyStatus.Pending);
    }

    [Fact]
    public async Task ListReadyOccurrencesAsync_SuccessorWaitsForPredecessorSuccess()
    {
        var (provider, predecessorId, successorId) = await CreateMaterializedChainAsync(JobDependencyFailurePolicy.KeepBlocked);
        var scheduler = provider.GetRequiredService<JobSchedulerService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        var before = await scheduler.ListReadyOccurrencesAsync();
        var execution = await scheduler.ExecuteStoredOccurrenceAsync(predecessorId);
        var successor = await store.Occurrences.GetAsync(successorId);
        var after = await scheduler.ListReadyOccurrencesAsync();

        before.Select(x => x.OccurrenceId).ShouldContain(predecessorId);
        before.Select(x => x.OccurrenceId).ShouldNotContain(successorId);
        execution.IsSuccess.ShouldBeTrue();
        successor.Status.ShouldBe(JobOccurrenceStatus.Due);
        successor.BlockedReason.ShouldBeNull();
        after.Select(x => x.OccurrenceId).ShouldContain(successorId);
    }

    [Fact]
    public async Task ExecuteStoredOccurrenceAsync_PredecessorRetry_KeepsSuccessorBlocked()
    {
        RetryThenSucceedDependencyJob.Reset(failuresBeforeSuccess: 1);
        var (provider, predecessorId, successorId) = await CreateMaterializedChainAsync(
            JobDependencyFailurePolicy.KeepBlocked,
            services =>
            {
                services.AddJobScheduler()
                    .WithJob<RetryThenSucceedDependencyJob>("predecessor", job => job
                        .Description("Retries once.")
                        .WithRetry(retry => retry.MaxAttempts(2))
                        .AddTrigger("once", trigger => trigger.At(new DateTimeOffset(2026, 05, 26, 09, 01, 00, TimeSpan.Zero)))
                        .Then("successor", chain => chain.WithTrigger("manual")))
                    .WithJob<SuccessfulDependencyJob>("successor", job => job
                        .Description("Runs second.")
                        .AddTrigger("manual", trigger => trigger.Manual()));
            });

        var scheduler = provider.GetRequiredService<JobSchedulerService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        var result = await scheduler.ExecuteStoredOccurrenceAsync(predecessorId);
        var predecessor = await store.Occurrences.GetAsync(predecessorId);
        var successor = await store.Occurrences.GetAsync(successorId);
        var dependency = (await store.Dependencies.ListByDependentAsync(successorId)).Single();

        result.IsSuccess.ShouldBeTrue();
        predecessor.Status.ShouldBe(JobOccurrenceStatus.RetryScheduled);
        successor.Status.ShouldBe(JobOccurrenceStatus.Blocked);
        dependency.Status.ShouldBe(JobDependencyStatus.Pending);
    }

    [Theory]
    [InlineData(JobDependencyFailurePolicy.KeepBlocked, JobOccurrenceStatus.Blocked)]
    [InlineData(JobDependencyFailurePolicy.Skip, JobOccurrenceStatus.Cancelled)]
    [InlineData(JobDependencyFailurePolicy.Cancel, JobOccurrenceStatus.Cancelled)]
    [InlineData(JobDependencyFailurePolicy.Fail, JobOccurrenceStatus.Failed)]
    public async Task ExecuteStoredOccurrenceAsync_PredecessorRetryExhaustion_AppliesDependencyFailurePolicy(JobDependencyFailurePolicy policy, JobOccurrenceStatus expectedStatus)
    {
        var (provider, predecessorId, successorId) = await CreateMaterializedChainAsync(
            policy,
            services =>
            {
                services.AddJobScheduler()
                    .WithJob<AlwaysFailDependencyJob>("predecessor", job => job
                        .Description("Fails terminally.")
                        .WithRetry(retry => retry.MaxAttempts(1))
                        .AddTrigger("once", trigger => trigger.At(new DateTimeOffset(2026, 05, 26, 09, 01, 00, TimeSpan.Zero)))
                        .Then("successor", chain => chain.WithTrigger("manual").WithFailurePolicy(policy)))
                    .WithJob<SuccessfulDependencyJob>("successor", job => job
                        .Description("Runs second.")
                        .AddTrigger("manual", trigger => trigger.Manual()));
            });

        var scheduler = provider.GetRequiredService<JobSchedulerService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        await scheduler.ExecuteStoredOccurrenceAsync(predecessorId);

        var successor = await store.Occurrences.GetAsync(successorId);
        var dependency = (await store.Dependencies.ListByDependentAsync(successorId)).Single();
        var history = await store.ExecutionHistory.ListAsync(successorId);

        successor.Status.ShouldBe(expectedStatus);
        if (policy == JobDependencyFailurePolicy.KeepBlocked)
        {
            dependency.Status.ShouldBe(JobDependencyStatus.Failed);
            successor.BlockedReason.ShouldNotBeNullOrWhiteSpace();
        }
        else if (policy == JobDependencyFailurePolicy.Skip)
        {
            dependency.Status.ShouldBe(JobDependencyStatus.Skipped);
        }
        else if (policy == JobDependencyFailurePolicy.Cancel)
        {
            dependency.Status.ShouldBe(JobDependencyStatus.Cancelled);
        }
        else
        {
            dependency.Status.ShouldBe(JobDependencyStatus.Failed);
        }

        history.Any(x => x.EventName.StartsWith("Dependency", StringComparison.Ordinal)).ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteStoredOccurrenceAsync_BlockedOccurrencesAreNotLeased()
    {
        var (provider, _, successorId) = await CreateMaterializedChainAsync(JobDependencyFailurePolicy.KeepBlocked);
        var scheduler = provider.GetRequiredService<JobSchedulerService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        var result = await scheduler.ExecuteStoredOccurrenceAsync(successorId);
        var leases = await store.Leases.ListAsync();

        result.IsFailure.ShouldBeTrue();
        leases.Any(x => x.OccurrenceId == successorId).ShouldBeFalse();
    }

    [Fact]
    public async Task DependencyState_SurvivesInMemoryProviderRestartSimulation()
    {
        var (provider, predecessorId, successorId) = await CreateMaterializedChainAsync(JobDependencyFailurePolicy.KeepBlocked);
        var restarted = new JobSchedulerService(
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<JobRegistrationStore>(),
            provider.GetRequiredService<IJobTriggerEvaluator>(),
            provider.GetRequiredService<IJobStoreProvider>(),
            provider.GetRequiredService<ISerializer>(),
            provider.GetRequiredService<JobEventSourceRegistry>(),
            provider.GetRequiredService<JobSchedulerHostedOptions>());
        var store = provider.GetRequiredService<IJobStoreProvider>();

        var ready = await restarted.ListReadyOccurrencesAsync();
        var successor = await store.Occurrences.GetAsync(successorId);

        ready.Select(x => x.OccurrenceId).ShouldContain(predecessorId);
        ready.Select(x => x.OccurrenceId).ShouldNotContain(successorId);
        successor.Status.ShouldBe(JobOccurrenceStatus.Blocked);
    }

    [Fact]
    public async Task BatchMembership_NeverCreatesDependencyLinks()
    {
        var provider = CreateProvider(services => services.AddJobScheduler());
        var store = provider.GetRequiredService<IJobStoreProvider>();
        var batchId = Guid.NewGuid();

        await store.Batches.TryCreateAsync(
            new JobBatch
            {
                BatchId = batchId,
                Description = "Batch without dependencies.",
                Status = JobBatchStatus.Created,
                CompletionPolicy = JobBatchCompletionPolicy.RequireAllSucceeded,
                CreatedDate = DateTimeOffset.UtcNow,
                UpdatedDate = DateTimeOffset.UtcNow,
            },
            [
                new JobBatchOccurrence
                {
                    BatchId = batchId,
                    OccurrenceId = Guid.NewGuid(),
                    ChildStatus = JobOccurrenceStatus.Materialized,
                    CreatedDate = DateTimeOffset.UtcNow,
                    UpdatedDate = DateTimeOffset.UtcNow,
                },
            ]);

        (await store.Queries.ListDependenciesAsync()).ShouldBeEmpty();
    }

    private ServiceProvider CreateProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        this.ConfigureLogging(services);
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2026, 05, 26, 09, 00, 00, TimeSpan.Zero)));
        configure(services);
        return services.BuildServiceProvider();
    }

    private async Task<(ServiceProvider Provider, Guid PredecessorId, Guid SuccessorId)> CreateMaterializedChainAsync(
        JobDependencyFailurePolicy policy,
        Action<IServiceCollection> configure = null)
    {
        var provider = configure is null
            ? CreateProvider(services =>
            {
                services.AddJobScheduler()
                    .WithJob<SuccessfulDependencyJob>("predecessor", job => job
                        .Description("Runs first.")
                        .AddTrigger("once", trigger => trigger.At(new DateTimeOffset(2026, 05, 26, 09, 01, 00, TimeSpan.Zero)))
                        .Then("successor", chain => chain.WithTrigger("manual").WithFailurePolicy(policy)))
                    .WithJob<SuccessfulDependencyJob>("successor", job => job
                        .Description("Runs second.")
                        .AddTrigger("manual", trigger => trigger.Manual()));
            })
            : CreateProvider(configure);

        var fakeTime = (FakeTimeProvider)provider.GetRequiredService<TimeProvider>();
        fakeTime.Advance(TimeSpan.FromMinutes(1));
        var scheduler = provider.GetRequiredService<JobSchedulerService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();
        await scheduler.MaterializeScheduledOccurrencesAsync(fakeTime.GetUtcNow(), 100);
        var occurrences = await store.Queries.ListOccurrencesAsync();
        return (provider, occurrences.Single(x => x.JobName == "predecessor").OccurrenceId, occurrences.Single(x => x.JobName == "successor").OccurrenceId);
    }

    private sealed class SuccessfulDependencyJob : JobBase
    {
        public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class RetryThenSucceedDependencyJob : JobBase
    {
        private static int remainingFailures;

        public static void Reset(int failuresBeforeSuccess)
        {
            remainingFailures = failuresBeforeSuccess;
        }

        public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Decrement(ref remainingFailures) >= 0)
            {
                return Task.FromResult(Result.Failure().WithError(new ValidationError("Retry required.")));
            }

            return Task.FromResult(Result.Success());
        }
    }

    private sealed class AlwaysFailDependencyJob : JobBase
    {
        public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Failure().WithError(new ValidationError("Terminal failure.")));
        }
    }
}
