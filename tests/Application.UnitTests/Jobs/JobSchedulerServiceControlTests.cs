// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Jobs;

using BridgingIT.DevKit.Application.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

public class JobSchedulerServiceControlTests(ITestOutputHelper output) : JobSchedulerTestBase(output)
{
    [Fact]
    public async Task DispatchAsync_FailedExecution_SchedulesRetryForSameOccurrenceAfterSweep()
    {
        RetryThenSucceedJob.Reset(failuresBeforeSuccess: 1);
        var provider = CreateProvider(services =>
        {
            services.AddJobScheduler().WithBackgroundExecution(options => options.EnableBackgroundExecution = false)
                .WithJob<RetryThenSucceedJob>("retry-job", job => job
                    .Description("Retries once.")
                    .WithRetry(retry => retry.MaxAttempts(2).FixedDelay(TimeSpan.FromMinutes(5)))
                    .AddTrigger("manual", trigger => trigger.Manual()));
        });

        var sut = provider.GetRequiredService<IJobSchedulerService>();
        var background = provider.GetRequiredService<JobSchedulerBackgroundService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();
        var fakeTime = (FakeTimeProvider)provider.GetRequiredService<TimeProvider>();

        var result = await sut.DispatchAsync<RetryThenSucceedJob>();
        await background.SweepOnceAsync();
        var occurrence = await store.Occurrences.GetAsync(result.Value.OccurrenceId);
        var executions = await store.Executions.ListByOccurrenceAsync(result.Value.OccurrenceId);

        occurrence.Status.ShouldBe(JobOccurrenceStatus.RetryScheduled);
        occurrence.DueUtc.ShouldBe(fakeTime.GetUtcNow().AddMinutes(5));
        executions.Count.ShouldBe(1);
        executions[0].Status.ShouldBe(JobExecutionStatus.Retried);
    }

    [Fact]
    public async Task RetryScheduledOccurrence_CreatesNewExecutionAttempt_NotNewOccurrence()
    {
        RetryThenSucceedJob.Reset(failuresBeforeSuccess: 1);
        var provider = CreateProvider(services =>
        {
            services.AddJobScheduler().WithBackgroundExecution(options => options.EnableBackgroundExecution = false)
                .WithJob<RetryThenSucceedJob>("retry-job", job => job
                    .Description("Retries once.")
                    .WithRetry(retry => retry.MaxAttempts(2).FixedDelay(TimeSpan.FromMinutes(1)))
                    .AddTrigger("manual", trigger => trigger.Manual()));
        });

        var scheduler = provider.GetRequiredService<IJobSchedulerService>();
        var background = provider.GetRequiredService<JobSchedulerBackgroundService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();
        var fakeTime = (FakeTimeProvider)provider.GetRequiredService<TimeProvider>();

        var dispatch = await scheduler.DispatchAsync<RetryThenSucceedJob>();
        await background.SweepOnceAsync();
        fakeTime.Advance(TimeSpan.FromMinutes(1));
        await background.SweepOnceAsync();

        var occurrences = await store.Queries.ListOccurrencesAsync();
        var executions = await store.Executions.ListByOccurrenceAsync(dispatch.Value.OccurrenceId);

        occurrences.Count.ShouldBe(1);
        occurrences[0].OccurrenceId.ShouldBe(dispatch.Value.OccurrenceId);
        executions.Count.ShouldBe(2);
        executions.Select(x => x.AttemptNumber).ShouldBe([1, 2]);
        executions.Select(x => x.Status).ShouldBe([JobExecutionStatus.Retried, JobExecutionStatus.Completed]);
    }

    [Fact]
    public async Task RetryScheduledOccurrence_Exhaustion_MarksOccurrenceFailed()
    {
        RetryThenSucceedJob.Reset(failuresBeforeSuccess: 3);
        var provider = CreateProvider(services =>
        {
            services.AddJobScheduler().WithBackgroundExecution(options => options.EnableBackgroundExecution = false)
                .WithJob<RetryThenSucceedJob>("retry-job", job => job
                    .Description("Eventually exhausts retries.")
                    .WithRetry(retry => retry.MaxAttempts(2).FixedDelay(TimeSpan.FromMinutes(1)))
                    .AddTrigger("manual", trigger => trigger.Manual()));
        });

        var scheduler = provider.GetRequiredService<IJobSchedulerService>();
        var background = provider.GetRequiredService<JobSchedulerBackgroundService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();
        var fakeTime = (FakeTimeProvider)provider.GetRequiredService<TimeProvider>();

        var dispatch = await scheduler.DispatchAsync<RetryThenSucceedJob>();
        await background.SweepOnceAsync();
        fakeTime.Advance(TimeSpan.FromMinutes(1));
        await background.SweepOnceAsync();

        var occurrence = await store.Occurrences.GetAsync(dispatch.Value.OccurrenceId);
        var executions = await store.Executions.ListByOccurrenceAsync(dispatch.Value.OccurrenceId);

        occurrence.Status.ShouldBe(JobOccurrenceStatus.Failed);
        executions.Count.ShouldBe(2);
        executions.Last().Status.ShouldBe(JobExecutionStatus.Failed);
    }

    [Fact]
    public async Task DispatchAndWaitAsync_Timeout_RecordsTimedOutExecutionOutcome()
    {
        TimeoutJob.Reset();
        var provider = CreateProvider(services =>
        {
            services.AddJobScheduler()
                .WithJob<TimeoutJob>("timeout-job", job => job
                    .Description("Times out.")
                    .WithTimeout(TimeSpan.FromMilliseconds(50))
                    .AddTrigger("manual", trigger => trigger.Manual()));
        });

        var sut = provider.GetRequiredService<IJobSchedulerService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        var result = await sut.DispatchAndWaitAsync<TimeoutJob>();
        var execution = (await store.Executions.ListByOccurrenceAsync(result.Value.OccurrenceId)).Single();
        var history = await store.ExecutionHistory.ListAsync(result.Value.OccurrenceId);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.TimedOut);
        execution.Status.ShouldBe(JobExecutionStatus.TimedOut);
        history.Any(x => x.EventName == "ExecutionTimedOut").ShouldBeTrue();
        TimeoutJob.WasCancelled.ShouldBeTrue();
    }

    [Fact]
    public async Task CancelOccurrenceAsync_BeforeExecution_MarksOccurrenceCancelled()
    {
        var provider = CreateProvider(services =>
        {
            services.AddJobScheduler().WithBackgroundExecution(options => options.EnableBackgroundExecution = false)
                .WithJob<SuccessfulControlJob>("scheduled-job", job => job
                    .Description("Scheduled work.")
                    .AddTrigger("scheduled", trigger => trigger.At(new DateTimeOffset(2026, 05, 26, 09, 05, 00, TimeSpan.Zero))));
        });

        var scheduler = provider.GetRequiredService<IJobSchedulerService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        var occurrence = new JobOccurrence
        {
            OccurrenceId = Guid.NewGuid(),
            OccurrenceKey = "scheduled-job:scheduled:cancel-before-start",
            JobName = "scheduled-job",
            TriggerName = "scheduled",
            TriggerType = JobTriggerType.OneTime,
            Status = JobOccurrenceStatus.Scheduled,
            DueUtc = new DateTimeOffset(2026, 05, 26, 09, 05, 00, TimeSpan.Zero),
            Data = Unit.Value,
            DataType = typeof(Unit),
            CreatedDate = new DateTimeOffset(2026, 05, 26, 09, 00, 00, TimeSpan.Zero),
            UpdatedDate = new DateTimeOffset(2026, 05, 26, 09, 00, 00, TimeSpan.Zero),
        };

        (await store.Occurrences.TryCreateAsync(occurrence)).ShouldBeTrue();

        var result = await scheduler.CancelOccurrenceAsync(occurrence.OccurrenceId, "operator cancel");
        var updated = await store.Occurrences.GetAsync(occurrence.OccurrenceId);

        result.IsSuccess.ShouldBeTrue();
        updated.Status.ShouldBe(JobOccurrenceStatus.Cancelled);
    }

    [Fact]
    public async Task CancelOccurrenceAsync_DuringExecution_PropagatesCancellationToken()
    {
        ControllableCancellationJob.Reset();
        var provider = CreateProvider(services =>
        {
            services.AddJobScheduler()
                .WithJob<ControllableCancellationJob>("cancel-job", job => job
                    .Description("Can be cancelled.")
                    .AddTrigger("manual", trigger => trigger.Manual()));
        });

        var scheduler = provider.GetRequiredService<IJobSchedulerService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        var executionTask = scheduler.DispatchAndWaitAsync<ControllableCancellationJob>();
        await ControllableCancellationJob.Started.Task;
        var occurrenceId = (await store.Queries.ListOccurrencesAsync()).Single().OccurrenceId;

        var cancel = await scheduler.CancelOccurrenceAsync(occurrenceId, "operator cancel");
        var result = await executionTask;

        cancel.IsSuccess.ShouldBeTrue();
        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Cancelled);
        ControllableCancellationJob.WasCancellationObserved.ShouldBeTrue();
    }

    [Fact]
    public async Task InterruptOccurrenceAsync_IsRecordedDistinctlyFromCancellation()
    {
        ControllableCancellationJob.Reset();
        var provider = CreateProvider(services =>
        {
            services.AddJobScheduler()
                .WithJob<ControllableCancellationJob>("interrupt-job", job => job
                    .Description("Can be interrupted.")
                    .AddTrigger("manual", trigger => trigger.Manual()));
        });

        var scheduler = provider.GetRequiredService<IJobSchedulerService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        var executionTask = scheduler.DispatchAndWaitAsync<ControllableCancellationJob>();
        await ControllableCancellationJob.Started.Task;
        var occurrenceId = (await store.Queries.ListOccurrencesAsync()).Single().OccurrenceId;

        var interrupt = await scheduler.InterruptOccurrenceAsync(occurrenceId, "operator interrupt");
        var result = await executionTask;
        var history = await store.ExecutionHistory.ListAsync(occurrenceId);

        interrupt.IsSuccess.ShouldBeTrue();
        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Interrupted);
        history.Any(x => x.EventName == "ExecutionInterrupted").ShouldBeTrue();
        history.Any(x => x.EventName == "ExecutionCancelled").ShouldBeFalse();
    }

    [Fact]
    public async Task PauseOccurrenceAsync_PreventsNewAttemptsUntilResume()
    {
        RetryThenSucceedJob.Reset(failuresBeforeSuccess: 1);
        var provider = CreateProvider(services =>
        {
            services.AddJobScheduler().WithBackgroundExecution(options => options.EnableBackgroundExecution = false)
                .WithJob<RetryThenSucceedJob>("retry-job", job => job
                    .Description("Retries once.")
                    .WithRetry(retry => retry.MaxAttempts(2).FixedDelay(TimeSpan.FromMinutes(1)))
                    .AddTrigger("manual", trigger => trigger.Manual()));
        });

        var scheduler = provider.GetRequiredService<IJobSchedulerService>();
        var background = provider.GetRequiredService<JobSchedulerBackgroundService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();
        var fakeTime = (FakeTimeProvider)provider.GetRequiredService<TimeProvider>();

        var dispatch = await scheduler.DispatchAsync<RetryThenSucceedJob>();
        await background.SweepOnceAsync();
        (await scheduler.PauseOccurrenceAsync(dispatch.Value.OccurrenceId)).IsSuccess.ShouldBeTrue();
        fakeTime.Advance(TimeSpan.FromMinutes(1));
        await background.SweepOnceAsync();

        (await store.Executions.ListByOccurrenceAsync(dispatch.Value.OccurrenceId)).Count.ShouldBe(1);
        (await scheduler.ResumeOccurrenceAsync(dispatch.Value.OccurrenceId)).IsSuccess.ShouldBeTrue();
        await background.SweepOnceAsync();
        (await store.Executions.ListByOccurrenceAsync(dispatch.Value.OccurrenceId)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task PauseJobAndTrigger_Resume_RestoresEligibility()
    {
        var provider = CreateProvider(services =>
        {
            services.AddJobScheduler()
                .WithJob<SuccessfulControlJob>("control-job", job => job
                    .Description("Control operations.")
                    .AddTrigger("manual", trigger => trigger.Manual()));
        });

        var scheduler = provider.GetRequiredService<IJobSchedulerService>();

        (await scheduler.PauseJobAsync("control-job")).IsSuccess.ShouldBeTrue();
        (await scheduler.DispatchAsync("control-job")).IsFailure.ShouldBeTrue();
        (await scheduler.ResumeJobAsync("control-job")).IsSuccess.ShouldBeTrue();
        (await scheduler.PauseTriggerAsync("control-job", "manual")).IsSuccess.ShouldBeTrue();
        (await scheduler.DispatchAsync("control-job")).IsFailure.ShouldBeTrue();
        (await scheduler.ResumeTriggerAsync("control-job", "manual")).IsSuccess.ShouldBeTrue();
        (await scheduler.DispatchAsync("control-job")).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task EnableJobAndTrigger_AllowsDispatchForDisabledRegistrations()
    {
        var provider = CreateProvider(services =>
        {
            services.AddJobScheduler()
                .WithJob<SuccessfulControlJob>("control-job", job =>
                {
                    job.Description("Control operations.");
                    job.Enabled(false);
                    job.AddTrigger("manual", trigger =>
                    {
                        trigger.Manual();
                        trigger.Enabled(false);
                    });
                });
        });

        var scheduler = provider.GetRequiredService<IJobSchedulerService>();

        (await scheduler.DispatchAsync("control-job")).IsFailure.ShouldBeTrue();
        (await scheduler.EnableJobAsync("control-job")).IsSuccess.ShouldBeTrue();
        (await scheduler.EnableTriggerAsync("control-job", "manual")).IsSuccess.ShouldBeTrue();
        (await scheduler.DispatchAsync("control-job")).IsSuccess.ShouldBeTrue();
        (await scheduler.DisableTriggerAsync("control-job", "manual")).IsSuccess.ShouldBeTrue();
        (await scheduler.DisableJobAsync("control-job")).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ReleaseOccurrenceLeaseAsync_RemovesPersistedLease()
    {
        var provider = CreateProvider(services =>
        {
            services.AddJobScheduler().WithBackgroundExecution(options => options.EnableBackgroundExecution = false)
                .WithJob<SuccessfulControlJob>("control-job", job => job
                    .Description("Control operations.")
                    .AddTrigger("manual", trigger => trigger.Manual()));
        });

        var scheduler = provider.GetRequiredService<IJobSchedulerService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();
        var nowUtc = provider.GetRequiredService<TimeProvider>().GetUtcNow();
        var occurrenceId = Guid.NewGuid();

        (await store.Occurrences.TryCreateAsync(new JobOccurrence
        {
            OccurrenceId = occurrenceId,
            OccurrenceKey = "control-job:manual:leased",
            JobName = "control-job",
            TriggerName = "manual",
            TriggerType = JobTriggerType.Manual,
            Status = JobOccurrenceStatus.Running,
            DueUtc = nowUtc,
            CreatedDate = nowUtc,
            UpdatedDate = nowUtc,
            Data = Unit.Value,
            DataType = typeof(Unit),
        })).ShouldBeTrue();

        await store.Leases.UpsertAsync(new JobLeaseRecord
        {
            OccurrenceId = occurrenceId,
            SchedulerInstanceId = "tests",
            OwnershipToken = "lease-token",
            AcquiredUtc = nowUtc,
            ExpiresUtc = nowUtc.AddMinutes(5),
            CreatedDate = nowUtc,
            UpdatedDate = nowUtc,
        });

        (await scheduler.ReleaseOccurrenceLeaseAsync(occurrenceId, "repair")).IsSuccess.ShouldBeTrue();
        (await store.Leases.GetAsync(occurrenceId)).ShouldBeNull();
    }

    [Fact]
    public async Task CancelOccurrencesAsync_AggregatesSuccessesAndFailures()
    {
        var provider = CreateProvider(services =>
        {
            services.AddJobScheduler().WithBackgroundExecution(options => options.EnableBackgroundExecution = false)
                .WithJob<SuccessfulControlJob>("control-job", job => job
                    .Description("Control operations.")
                    .AddTrigger("manual", trigger => trigger.Manual()));
        });

        var scheduler = provider.GetRequiredService<IJobSchedulerService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();
        var nowUtc = provider.GetRequiredService<TimeProvider>().GetUtcNow();
        var cancellableId = Guid.NewGuid();
        var completedId = Guid.NewGuid();

        (await store.Occurrences.TryCreateAsync(new JobOccurrence
        {
            OccurrenceId = cancellableId,
            OccurrenceKey = "control-job:manual:bulk-cancel-1",
            JobName = "control-job",
            TriggerName = "manual",
            TriggerType = JobTriggerType.Manual,
            Status = JobOccurrenceStatus.Scheduled,
            DueUtc = nowUtc,
            CreatedDate = nowUtc,
            UpdatedDate = nowUtc,
            Data = Unit.Value,
            DataType = typeof(Unit),
        })).ShouldBeTrue();

        (await store.Occurrences.TryCreateAsync(new JobOccurrence
        {
            OccurrenceId = completedId,
            OccurrenceKey = "control-job:manual:bulk-cancel-2",
            JobName = "control-job",
            TriggerName = "manual",
            TriggerType = JobTriggerType.Manual,
            Status = JobOccurrenceStatus.Completed,
            DueUtc = nowUtc,
            CreatedDate = nowUtc,
            UpdatedDate = nowUtc,
            Data = Unit.Value,
            DataType = typeof(Unit),
        })).ShouldBeTrue();

        var result = await scheduler.CancelOccurrencesAsync([cancellableId, completedId], "bulk cancel");

        result.IsSuccess.ShouldBeTrue();
        result.Value.RequestedCount.ShouldBe(2);
        result.Value.SucceededCount.ShouldBe(1);
        result.Value.FailedCount.ShouldBe(1);
        (await store.Occurrences.GetAsync(cancellableId)).Status.ShouldBe(JobOccurrenceStatus.Cancelled);
    }

    [Fact]
    public async Task InvalidLifecycleTransitions_ReturnResultFailure()
    {
        var provider = CreateProvider(services =>
        {
            services.AddJobScheduler()
                .WithJob<SuccessfulControlJob>("control-job", job => job
                    .Description("Control operations.")
                    .AddTrigger("manual", trigger => trigger.Manual()));
        });

        var scheduler = provider.GetRequiredService<IJobSchedulerService>();
        var dispatch = await scheduler.DispatchAndWaitAsync<SuccessfulControlJob>();

        (await scheduler.CancelOccurrenceAsync(dispatch.Value.OccurrenceId)).IsFailure.ShouldBeTrue();
        (await scheduler.PauseOccurrenceAsync(dispatch.Value.OccurrenceId)).IsFailure.ShouldBeTrue();
        (await scheduler.ResumeJobAsync("control-job")).IsFailure.ShouldBeTrue();
        (await scheduler.ArchiveOccurrenceAsync(dispatch.Value.OccurrenceId)).IsSuccess.ShouldBeTrue();
        (await scheduler.ArchiveOccurrenceAsync(dispatch.Value.OccurrenceId)).IsFailure.ShouldBeTrue();
    }

    private ServiceProvider CreateProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        this.ConfigureLogging(services);
        services.AddSingleton<IHostApplicationLifetime, TestHostApplicationLifetime>();
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2026, 05, 26, 09, 00, 00, TimeSpan.Zero)));
        configure(services);
        return services.BuildServiceProvider();
    }

    private sealed class SuccessfulControlJob : JobBase
    {
        public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class RetryThenSucceedJob : JobBase
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
                return Task.FromResult(Result.Failure().WithError(new ValidationError("retry me")));
            }

            return Task.FromResult(Result.Success());
        }
    }

    private sealed class TimeoutJob : JobBase
    {
        public static bool WasCancelled;

        public static void Reset()
        {
            WasCancelled = false;
        }

        public override async Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return Result.Success();
            }
            catch (OperationCanceledException)
            {
                WasCancelled = true;
                throw;
            }
        }
    }

    private sealed class ControllableCancellationJob : JobBase
    {
        public static TaskCompletionSource<bool> Started { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public static bool WasCancellationObserved;

        public static void Reset()
        {
            Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
            WasCancellationObserved = false;
        }

        public override async Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult(true);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return Result.Success();
            }
            catch (OperationCanceledException)
            {
                WasCancellationObserved = true;
                throw;
            }
        }
    }

    private sealed class TestHostApplicationLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource started = new();
        private readonly CancellationTokenSource stopping = new();
        private readonly CancellationTokenSource stopped = new();

        public CancellationToken ApplicationStarted => this.started.Token;

        public CancellationToken ApplicationStopping => this.stopping.Token;

        public CancellationToken ApplicationStopped => this.stopped.Token;

        public void StopApplication()
        {
            if (!this.stopping.IsCancellationRequested)
            {
                this.stopping.Cancel();
            }

            if (!this.stopped.IsCancellationRequested)
            {
                this.stopped.Cancel();
            }
        }
    }
}
