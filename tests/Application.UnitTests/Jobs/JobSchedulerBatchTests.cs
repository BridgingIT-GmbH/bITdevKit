// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Jobs;

using BridgingIT.DevKit.Application.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

public class JobSchedulerBatchTests(ITestOutputHelper output) : JobSchedulerTestBase(output)
{
    [Fact]
    public async Task CreateBatchAsync_EmptyBatch_CreatesCompletedBatchWithoutChildren()
    {
        var provider = CreateProvider();
        var sut = provider.GetRequiredService<IJobSchedulerService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        var result = await sut.CreateBatchAsync(new JobBatchCreateRequest { BatchId = "batch-empty" });
        var batch = await GetBatchAsync(store, result.Value.BatchId);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AcceptedCount.ShouldBe(0);
        batch.Status.ShouldBe(JobBatchStatus.Completed);
        batch.CompletedDate.ShouldNotBeNull();
        (await store.Batches.ListOccurrencesAsync(batch.BatchId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task DispatchBatchAsync_InvalidChild_FailsWithoutPersistingRunnableOrphans()
    {
        var provider = CreateProvider();
        var sut = provider.GetRequiredService<IJobSchedulerService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        var result = await sut.DispatchBatchAsync(new JobBatchDispatchRequest
        {
            BatchId = "batch-invalid",
            Items =
            [
                new JobBatchDispatchItem { JobName = "success", Data = Unit.Value },
                new JobBatchDispatchItem { JobName = "missing-job", Data = Unit.Value },
            ],
        });

        result.IsFailure.ShouldBeTrue();
        (await store.Queries.ListOccurrencesAsync()).ShouldBeEmpty();
        (await store.Batches.ListAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task DispatchBatchAsync_EmptyItems_CreatesCompletedBatchWithoutChildren()
    {
        var provider = CreateProvider();
        var sut = provider.GetRequiredService<IJobSchedulerService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        var result = await sut.DispatchBatchAsync(new JobBatchDispatchRequest { BatchId = "batch-empty-dispatch" });
        var batch = await GetBatchAsync(store, result.Value.BatchId);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobBatchStatus.Completed);
        result.Value.OccurrenceIds.ShouldBeEmpty();
        batch.Status.ShouldBe(JobBatchStatus.Completed);
        batch.CompletedDate.ShouldNotBeNull();
        (await store.Batches.ListOccurrencesAsync(batch.BatchId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task DispatchBatchAsync_ValidItems_CreatesBatchOccurrencesAndMembershipAtomically()
    {
        var provider = CreateProvider();
        var sut = provider.GetRequiredService<IJobSchedulerService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        var result = await sut.DispatchBatchAsync(new JobBatchDispatchRequest
        {
            BatchId = "batch-dispatch",
            Items =
            [
                new JobBatchDispatchItem { JobName = "success", Data = Unit.Value, Sequence = 1 },
                new JobBatchDispatchItem { JobName = "success", Data = Unit.Value, Sequence = 2 },
            ],
        });
        var batch = await GetBatchAsync(store, result.Value.BatchId);
        var memberships = await store.Batches.ListOccurrencesAsync(batch.BatchId);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AcceptedCount.ShouldBe(2);
        memberships.Count.ShouldBe(2);
        (await store.Queries.ListOccurrencesAsync()).Count.ShouldBe(2);
        batch.Status.ShouldBe(JobBatchStatus.Processing);
    }

    [Fact]
    public async Task DispatchBatchAsync_ValidItems_WritesBatchHistory()
    {
        var provider = CreateProvider();
        var sut = provider.GetRequiredService<IJobSchedulerService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        var result = await sut.DispatchBatchAsync(new JobBatchDispatchRequest
        {
            BatchId = "batch-history",
            Items =
            [
                new JobBatchDispatchItem { JobName = "success", Data = Unit.Value },
                new JobBatchDispatchItem { JobName = "success", Data = Unit.Value },
            ],
        });
        var batch = await GetBatchAsync(store, result.Value.BatchId);
        var history = await store.BatchHistory.ListAsync(batch.BatchId);

        result.IsSuccess.ShouldBeTrue();
        history.ShouldContain(x => x.EventName == "BatchDispatched");
        history.Single(x => x.EventName == "BatchDispatched").Properties["acceptedCount"].ShouldBe(2);
    }

    [Fact]
    public async Task AttachToBatchAsync_CompletedBatch_AddsChildrenAndReturnsToProcessing()
    {
        var provider = CreateProvider();
        var scheduler = provider.GetRequiredService<JobSchedulerService>();
        var sut = provider.GetRequiredService<IJobSchedulerService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        var dispatch = await sut.DispatchBatchAsync(new JobBatchDispatchRequest
        {
            BatchId = "batch-attach",
            Items = [new JobBatchDispatchItem { JobName = "success", Data = Unit.Value }],
        });
        await scheduler.ExecuteStoredOccurrenceAsync(dispatch.Value.OccurrenceIds.Single());

        var attach = await sut.AttachToBatchAsync(dispatch.Value.BatchId, new JobBatchDispatchRequest
        {
            IdempotencyKey = "attach-1",
            Items = [new JobBatchDispatchItem { JobName = "success", Data = Unit.Value }],
        });
        var batch = await GetBatchAsync(store, dispatch.Value.BatchId);

        attach.IsSuccess.ShouldBeTrue();
        attach.Value.AcceptedCount.ShouldBe(2);
        batch.Status.ShouldBe(JobBatchStatus.Processing);
    }

    [Fact]
    public async Task BatchStatus_RequireAllSucceeded_FailedChildYieldsFailed()
    {
        var provider = CreateProvider();
        var scheduler = provider.GetRequiredService<JobSchedulerService>();
        var sut = provider.GetRequiredService<IJobSchedulerService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        var dispatch = await sut.DispatchBatchAsync(new JobBatchDispatchRequest
        {
            BatchId = "batch-failed",
            CompletionPolicy = JobBatchCompletionPolicy.RequireAllSucceeded,
            Items =
            [
                new JobBatchDispatchItem { JobName = "success", Data = Unit.Value },
                new JobBatchDispatchItem { JobName = "fail", Data = Unit.Value },
            ],
        });

        foreach (var occurrenceId in dispatch.Value.OccurrenceIds)
        {
            await scheduler.ExecuteStoredOccurrenceAsync(occurrenceId);
        }

        var batch = await GetBatchAsync(store, dispatch.Value.BatchId);
        batch.Status.ShouldBe(JobBatchStatus.Failed);
    }

    [Fact]
    public async Task BatchStatus_AllowPartialCompletion_FailedChildYieldsCompletedWithFailures()
    {
        var provider = CreateProvider();
        var scheduler = provider.GetRequiredService<JobSchedulerService>();
        var sut = provider.GetRequiredService<IJobSchedulerService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        var dispatch = await sut.DispatchBatchAsync(new JobBatchDispatchRequest
        {
            BatchId = "batch-partial",
            CompletionPolicy = JobBatchCompletionPolicy.AllowPartialCompletion,
            Items =
            [
                new JobBatchDispatchItem { JobName = "success", Data = Unit.Value },
                new JobBatchDispatchItem { JobName = "fail", Data = Unit.Value },
            ],
        });

        foreach (var occurrenceId in dispatch.Value.OccurrenceIds)
        {
            await scheduler.ExecuteStoredOccurrenceAsync(occurrenceId);
        }

        var batch = await GetBatchAsync(store, dispatch.Value.BatchId);
        batch.Status.ShouldBe(JobBatchStatus.CompletedWithFailures);
    }

    [Fact]
    public async Task RetryBatchAsync_RetriesEligibleFailedChildrenOnly()
    {
        var provider = CreateProvider();
        var scheduler = provider.GetRequiredService<JobSchedulerService>();
        var sut = provider.GetRequiredService<IJobSchedulerService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        var dispatch = await sut.DispatchBatchAsync(new JobBatchDispatchRequest
        {
            BatchId = "batch-retry",
            Items =
            [
                new JobBatchDispatchItem { JobName = "success", Data = Unit.Value },
                new JobBatchDispatchItem { JobName = "fail", Data = Unit.Value },
            ],
        });

        foreach (var occurrenceId in dispatch.Value.OccurrenceIds)
        {
            await scheduler.ExecuteStoredOccurrenceAsync(occurrenceId);
        }

        var retry = await sut.RetryBatchAsync(dispatch.Value.BatchId, "retry batch");
        var occurrences = await store.Queries.ListOccurrencesAsync();

        retry.IsSuccess.ShouldBeTrue();
        retry.Value.RequestedCount.ShouldBe(1);
        retry.Value.SucceededCount.ShouldBe(1);
        occurrences.Count(x => x.Status == JobOccurrenceStatus.RetryScheduled).ShouldBe(1);
        occurrences.Count(x => x.Status == JobOccurrenceStatus.Completed).ShouldBe(1);
    }

    [Fact]
    public async Task CancelBatchAsync_PreventsNotYetStartedChildrenFromExecuting()
    {
        var provider = CreateProvider();
        var scheduler = provider.GetRequiredService<JobSchedulerService>();
        var sut = provider.GetRequiredService<IJobSchedulerService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        var dispatch = await sut.DispatchBatchAsync(new JobBatchDispatchRequest
        {
            BatchId = "batch-cancel",
            Items = [new JobBatchDispatchItem { JobName = "success", Data = Unit.Value }],
        });

        (await sut.CancelBatchAsync(dispatch.Value.BatchId, "cancel batch")).IsSuccess.ShouldBeTrue();
        var execution = await scheduler.ExecuteStoredOccurrenceAsync(dispatch.Value.OccurrenceIds.Single());
        var occurrence = await store.Occurrences.GetAsync(dispatch.Value.OccurrenceIds.Single());

        execution.IsFailure.ShouldBeTrue();
        occurrence.Status.ShouldBe(JobOccurrenceStatus.Cancelled);
    }

    [Fact]
    public async Task PauseResumeBatchAsync_MapsToEligibleChildren()
    {
        var provider = CreateProvider();
        var sut = provider.GetRequiredService<IJobSchedulerService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        var dispatch = await sut.DispatchBatchAsync(new JobBatchDispatchRequest
        {
            BatchId = "batch-pause",
            Items = [new JobBatchDispatchItem { JobName = "success", Data = Unit.Value }],
        });

        (await sut.PauseBatchAsync(dispatch.Value.BatchId, "pause batch")).IsSuccess.ShouldBeTrue();
        (await store.Occurrences.GetAsync(dispatch.Value.OccurrenceIds.Single())).Status.ShouldBe(JobOccurrenceStatus.Paused);

        (await sut.ResumeBatchAsync(dispatch.Value.BatchId)).IsSuccess.ShouldBeTrue();
        (await store.Occurrences.GetAsync(dispatch.Value.OccurrenceIds.Single())).Status.ShouldBe(JobOccurrenceStatus.Due);
    }

    [Fact]
    public async Task ArchiveBatchAsync_RequiresTerminalChildrenAndArchivesRetentionState()
    {
        var provider = CreateProvider();
        var scheduler = provider.GetRequiredService<JobSchedulerService>();
        var sut = provider.GetRequiredService<IJobSchedulerService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        var dispatch = await sut.DispatchBatchAsync(new JobBatchDispatchRequest
        {
            BatchId = "batch-archive",
            Items = [new JobBatchDispatchItem { JobName = "success", Data = Unit.Value }],
        });

        (await sut.ArchiveBatchAsync(dispatch.Value.BatchId, "too early")).IsFailure.ShouldBeTrue();
        await scheduler.ExecuteStoredOccurrenceAsync(dispatch.Value.OccurrenceIds.Single());
        (await sut.ArchiveBatchAsync(dispatch.Value.BatchId, "archive batch")).IsSuccess.ShouldBeTrue();

        var batch = await GetBatchAsync(store, dispatch.Value.BatchId);
        var occurrence = await store.Occurrences.GetAsync(dispatch.Value.OccurrenceIds.Single());
        batch.Status.ShouldBe(JobBatchStatus.Archived);
        occurrence.Status.ShouldBe(JobOccurrenceStatus.Archived);
    }

    [Fact]
    public async Task ArchiveBatchAsync_MixedArchivedAndCompletedChildren_RecomputesArchiveRollups()
    {
        var provider = CreateProvider();
        var scheduler = provider.GetRequiredService<JobSchedulerService>();
        var sut = provider.GetRequiredService<IJobSchedulerService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        var dispatch = await sut.DispatchBatchAsync(new JobBatchDispatchRequest
        {
            BatchId = "batch-archive-mixed",
            Items =
            [
                new JobBatchDispatchItem { JobName = "success", Data = Unit.Value },
                new JobBatchDispatchItem { JobName = "success", Data = Unit.Value },
            ],
        });

        await scheduler.ExecuteStoredOccurrenceAsync(dispatch.Value.OccurrenceIds[0]);
        await scheduler.ExecuteStoredOccurrenceAsync(dispatch.Value.OccurrenceIds[1]);
        (await sut.ArchiveOccurrenceAsync(dispatch.Value.OccurrenceIds[0], "archive first child")).IsSuccess.ShouldBeTrue();

        (await sut.ArchiveBatchAsync(dispatch.Value.BatchId, "archive mixed batch")).IsSuccess.ShouldBeTrue();

        var batch = await GetBatchAsync(store, dispatch.Value.BatchId);
        var memberships = await store.Batches.ListOccurrencesAsync(batch.BatchId);

        batch.Status.ShouldBe(JobBatchStatus.Archived);
        batch.AcceptedCount.ShouldBe(2);
        batch.ArchivedCount.ShouldBe(2);
        batch.SucceededCount.ShouldBe(0);
        memberships.All(x => x.ChildStatus == JobOccurrenceStatus.Archived).ShouldBeTrue();
    }

    [Fact]
    public async Task ArchiveBatchAsync_AlreadyArchivedBatch_FailsClearly()
    {
        var provider = CreateProvider();
        var scheduler = provider.GetRequiredService<JobSchedulerService>();
        var sut = provider.GetRequiredService<IJobSchedulerService>();

        var dispatch = await sut.DispatchBatchAsync(new JobBatchDispatchRequest
        {
            BatchId = "batch-archive-twice",
            Items = [new JobBatchDispatchItem { JobName = "success", Data = Unit.Value }],
        });

        await scheduler.ExecuteStoredOccurrenceAsync(dispatch.Value.OccurrenceIds.Single());
        (await sut.ArchiveBatchAsync(dispatch.Value.BatchId, "first archive")).IsSuccess.ShouldBeTrue();

        var result = await sut.ArchiveBatchAsync(dispatch.Value.BatchId, "second archive");

        result.IsFailure.ShouldBeTrue();
        result.Errors.First().Message.ShouldContain("already archived");
    }

    private ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        this.ConfigureLogging(services);
        services.AddSingleton<IHostApplicationLifetime, TestHostApplicationLifetime>();
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2026, 05, 26, 09, 00, 00, TimeSpan.Zero)));
        services.AddJobScheduler().WithBackgroundExecution(options => options.EnableBackgroundExecution = false)
            .WithJob<SuccessfulBatchJob>("success", job => job
                .Description("Succeeds.")
                .AddTrigger("manual", trigger => trigger.Manual()))
            .WithJob<FailingBatchJob>("fail", job => job
                .Description("Fails.")
                .AddTrigger("manual", trigger => trigger.Manual()));
        return services.BuildServiceProvider();
    }

    private static async Task<JobBatch> GetBatchAsync(IJobStoreProvider store, string batchId)
    {
        return (await store.Batches.ListAsync()).Single(x => x.ExternalBatchId == batchId);
    }

    private sealed class SuccessfulBatchJob : JobBase
    {
        public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class FailingBatchJob : JobBase
    {
        public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Failure().WithError(new ValidationError("batch failed")));
        }
    }

    private sealed class TestHostApplicationLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => CancellationToken.None;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication()
        {
        }
    }
}
