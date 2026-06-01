// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Jobs;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

public class JobTestHarnessTests(ITestOutputHelper output) : JobSchedulerTestBase(output)
{
    [Fact]
    public async Task EchoJobCanRunWithSyntheticExecutionContext()
    {
        var context = new JobExecutionContextBuilder<EchoJobData>()
            .WithJobName("synthetic")
            .WithTriggerName("manual")
            .WithCorrelationId("corr-42")
            .WithData(new EchoJobData { Message = "customer-42" })
            .Build();
        var sut = new EchoJob();

        var result = await sut.ExecuteAsync(context);

        result.IsSuccess.ShouldBeTrue();
        context.ShouldHaveMessages("customer-42");
        context.ShouldHaveItem("echo.message", "customer-42");
        context.ShouldHaveItem("echo.correlationId", "corr-42");
    }

    [Fact]
    public async Task SchedulerHarnessCanDispatchReusableEchoJob()
    {
        using var harness = this.CreateHarness(jobs =>
            jobs.WithJob<EchoJob>("echo-job", job => job
                .Description("echoes the payload for tests")
                .AddTrigger("manual", trigger => trigger.Manual())));

        var result = await harness.DispatchAndWaitAsync<EchoJob>(
            new EchoJobData { Message = "hello-jobs" },
            new JobDispatchOptions { CorrelationId = "corr-echo" });

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Completed);
        result.Value.Messages.ShouldContain("hello-jobs");
    }

    [Fact]
    public async Task HarnessCanMaterializeCronManualDelayedAndStartupTriggers()
    {
        using var harness = this.CreateHarness(jobs =>
        {
            jobs.WithJob<NoOpJob>("cron-job", job => job.Description("cron").AddTrigger("cron", trigger => trigger.Cron("* * * * *").WithMissedOccurrencePolicy(JobMissedOccurrencePolicy.RunAll)));
            jobs.WithJob<NoOpJob>("delayed-job", job => job.Description("delayed").AddTrigger("delayed", trigger => trigger.After(TimeSpan.FromMinutes(10))));
            jobs.WithJob<NoOpJob>("startup-job", job => job.Description("startup").AddTrigger("startup", trigger => trigger.StartupDelay(TimeSpan.FromMinutes(3))));
            jobs.WithJob<NoOpJob>("manual-job", job => job.Description("manual").AddTrigger("manual", trigger => trigger.Manual()));
        });

        (await harness.MaterializeAsync()).IsSuccess.ShouldBeTrue();
        harness.Advance(TimeSpan.FromMinutes(3));
        (await harness.MaterializeAsync()).IsSuccess.ShouldBeTrue();
        harness.Advance(TimeSpan.FromMinutes(7));
        var materialized = await harness.MaterializeAsync();
        var manualDispatch = await harness.DispatchAsync("manual-job");

        materialized.IsSuccess.ShouldBeTrue();
        (await harness.FindOccurrenceAsync("cron-job", "cron")).ShouldNotBeNull();
        (await harness.FindOccurrenceAsync("delayed-job", "delayed")).ShouldNotBeNull();
        (await harness.FindOccurrenceAsync("startup-job", "startup")).ShouldNotBeNull();
        manualDispatch.IsSuccess.ShouldBeTrue();
        (await harness.FindOccurrenceAsync("manual-job", "manual")).ShouldNotBeNull();
    }

    [Fact]
    public async Task HarnessCanAdvanceFakeTimeDeterministically()
    {
        using var harness = this.CreateHarness(jobs =>
            jobs.WithJob<NoOpJob>("delayed-job", job => job
                .Description("delayed")
                .AddTrigger("delayed", trigger => trigger.After(TimeSpan.FromMinutes(10)))));

        await harness.MaterializeAsync();

        (await harness.ListReadyOccurrencesAsync()).ShouldBeEmpty();
        harness.Advance(TimeSpan.FromMinutes(10));
        await harness.MaterializeAsync();

        (await harness.ListReadyOccurrencesAsync()).ShouldContain(x => x.JobName == "delayed-job");
    }

    [Fact]
    public async Task HarnessCanAssertHistoryAndUseInjectedFakes()
    {
        var recorder = new RecordingDependency();
        using var harness = this.CreateHarness(
            jobs => jobs.WithJob<RecordingJob>("recording-job", job => job
                .Description("records")
                .AddTrigger("manual", trigger => trigger.Manual())),
            services => services.AddSingleton<IRecordingDependency>(recorder));

        var dispatch = await harness.DispatchAndWaitAsync<RecordingJob>();

        dispatch.IsSuccess.ShouldBeTrue();
        recorder.Messages.ShouldBe(["recording-job"]);
        await harness.AssertHistoryContainsAsync(dispatch.Value.OccurrenceId, "ExecutionCompleted");
        await harness.AssertHistoryContainsAsync(dispatch.Value.OccurrenceId, "OccurrenceCompleted");
    }

    [Fact]
    public async Task HarnessCanAssertRetryAttempts()
    {
        RetryHarnessJob.Reset(failuresBeforeSuccess: 1);
        using var harness = this.CreateHarness(jobs =>
            jobs.WithJob<RetryHarnessJob>("retry-job", job => job
                .Description("retries")
                .WithRetry(retry => retry.MaxAttempts(2).FixedDelay(TimeSpan.FromMinutes(5)))
                .AddTrigger("manual", trigger => trigger.Manual())));

        var dispatch = await harness.DispatchAsync<RetryHarnessJob>();

        dispatch.IsSuccess.ShouldBeTrue();
        var firstAttempt = await harness.ExecuteOccurrenceAsync(dispatch.Value.OccurrenceId);
        firstAttempt.IsSuccess.ShouldBeTrue();
        firstAttempt.Value.Status.ShouldBe(JobExecutionStatus.Retried);
        await harness.AssertRetryAttemptsAsync(dispatch.Value.OccurrenceId, 1, JobOccurrenceStatus.RetryScheduled);

        harness.Advance(TimeSpan.FromMinutes(5));
        await harness.SweepAsync();

        await harness.AssertRetryAttemptsAsync(dispatch.Value.OccurrenceId, 2, JobOccurrenceStatus.Completed);
    }

    [Fact]
    public async Task HarnessCanAssertBlockedDependencies()
    {
        using var harness = this.CreateHarness(jobs =>
        {
            jobs.WithJob<NoOpJob>("predecessor", job => job
                .Description("first")
                .AddTrigger("once", trigger => trigger.At(new DateTimeOffset(2026, 05, 26, 09, 01, 00, TimeSpan.Zero)))
                .Then("successor", chain => chain.WithTrigger("manual")));
            jobs.WithJob<NoOpJob>("successor", job => job
                .Description("second")
                .AddTrigger("manual", trigger => trigger.Manual()));
        });

        harness.Advance(TimeSpan.FromMinutes(1));
        await harness.MaterializeAsync();
        var successor = await harness.FindOccurrenceAsync("successor", "manual");

        successor.ShouldNotBeNull();
        await harness.AssertBlockedDependencyAsync(successor.OccurrenceId, "Waiting for prerequisite occurrence");
    }

    [Fact]
    public async Task HarnessCanAssertBatchRollup()
    {
        using var harness = this.CreateHarness(jobs =>
        {
            jobs.WithJob<NoOpJob>("success-job", job => job.Description("success").AddTrigger("manual", trigger => trigger.Manual()));
            jobs.WithJob<FailingJob>("fail-job", job => job.Description("fail").AddTrigger("manual", trigger => trigger.Manual()));
        });

        var dispatch = await harness.Scheduler.DispatchBatchAsync(new JobBatchDispatchRequest
        {
            BatchId = "batch-rollup",
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

        await harness.AssertBatchStatusAsync("batch-rollup", JobBatchStatus.CompletedWithFailures, 2);
    }

    [Fact]
    public async Task HarnessCanExerciseTimeoutBehaviorWithoutRealSleeping()
    {
        using var harness = this.CreateHarness(jobs =>
            jobs.WithJob<TimeoutHarnessJob>("timeout-job", job => job
                .Description("times out")
                .WithTimeout(TimeSpan.FromMilliseconds(50))
                .AddTrigger("manual", trigger => trigger.Manual())));

        var result = await harness.DispatchAndWaitAsync<TimeoutHarnessJob>();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.TimedOut);
        await harness.AssertHistoryContainsAsync(result.Value.OccurrenceId, "ExecutionTimedOut");
    }

    private sealed class NoOpJob : JobBase
    {
        public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
        {
            context.Messages.Add("noop");
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class FailingJob : JobBase
    {
        public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Failure().WithError(new ValidationError("failed")));
        }
    }

    private sealed class RetryHarnessJob : JobBase
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

    private interface IRecordingDependency
    {
        void Record(string value);
    }

    private sealed class RecordingDependency : IRecordingDependency
    {
        public List<string> Messages { get; } = [];

        public void Record(string value)
        {
            this.Messages.Add(value);
        }
    }

    private sealed class RecordingJob(IRecordingDependency dependency) : JobBase
    {
        public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
        {
            dependency.Record(context.JobName);
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class TimeoutHarnessJob : JobBase
    {
        public override async Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Result.Success();
        }
    }
}
