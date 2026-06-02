// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Jobs;

using BridgingIT.DevKit.Application.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

public class JobSchedulerBackgroundServiceTests(ITestOutputHelper output) : JobSchedulerTestBase(output)
{
    [Fact]
    public async Task SweepOnceAsync_DueOccurrencesExecuteInPriorityThenDueOrder()
    {
        OrderedBackgroundJob.ExecutionOrder.Clear();
        var dueSoonUtc = new DateTimeOffset(2026, 05, 26, 09, 01, 00, TimeSpan.Zero);
        var dueLaterUtc = new DateTimeOffset(2026, 05, 26, 09, 02, 00, TimeSpan.Zero);
        var provider = CreateProvider(
            options =>
            {
                options.EnableBackgroundExecution = false;
                options.MaxConcurrency = 1;
                options.BatchSize = 10;
            },
            services =>
            {
                services.AddJobScheduler().WithJob<OrderedBackgroundJob>("ordered", job => job
                    .Description("Orders occurrences.")
                    .AddTrigger("low", trigger => trigger.At(dueLaterUtc).Priority(10))
                    .AddTrigger("high", trigger => trigger.At(dueSoonUtc).Priority(50))
                    .AddTrigger("mid", trigger => trigger.At(dueSoonUtc).Priority(25)));
            });

        var fakeTime = (FakeTimeProvider)provider.GetRequiredService<TimeProvider>();
        var sut = provider.GetRequiredService<JobSchedulerBackgroundService>();

        await sut.SweepOnceAsync();
        fakeTime.Advance(TimeSpan.FromMinutes(1));
        await sut.SweepOnceAsync();
        await sut.SweepOnceAsync();
        fakeTime.Advance(TimeSpan.FromMinutes(1));
        await sut.SweepOnceAsync();

        OrderedBackgroundJob.ExecutionOrder.ShouldBe(["high", "mid", "low"]);
    }

    [Fact]
    public async Task SweepOnceAsync_BatchSizeLimitsScan()
    {
        OrderedBackgroundJob.ExecutionOrder.Clear();
        var dueUtc = new DateTimeOffset(2026, 05, 26, 09, 01, 00, TimeSpan.Zero);
        var provider = CreateProvider(
            options =>
            {
                options.EnableBackgroundExecution = false;
                options.MaxConcurrency = 1;
                options.BatchSize = 1;
            },
            services =>
            {
                services.AddJobScheduler().WithJob<OrderedBackgroundJob>("ordered", job => job
                    .Description("Orders occurrences.")
                    .AddTrigger("first", trigger => trigger.At(dueUtc).Priority(10))
                    .AddTrigger("second", trigger => trigger.At(dueUtc).Priority(9)));
            });

        var fakeTime = (FakeTimeProvider)provider.GetRequiredService<TimeProvider>();
        var sut = provider.GetRequiredService<JobSchedulerBackgroundService>();

        await sut.SweepOnceAsync();
        fakeTime.Advance(TimeSpan.FromMinutes(1));
        await sut.SweepOnceAsync();
        OrderedBackgroundJob.ExecutionOrder.Count.ShouldBe(1);
        await sut.SweepOnceAsync();
        OrderedBackgroundJob.ExecutionOrder.Count.ShouldBe(2);
    }

    [Fact]
    public async Task SweepOnceAsync_CronTriggerWithDefaultMissedPolicy_MaterializesAndExecutesOccurrence()
    {
        var provider = CreateProvider(
            options =>
            {
                options.EnableBackgroundExecution = false;
                options.MaxConcurrency = 1;
                options.BatchSize = 10;
            },
            services =>
            {
                services.AddJobScheduler().WithJob<SuccessfulBackgroundJob>("cron", job => job
                    .Description("Cron work.")
                    .AddTrigger("every-minute", trigger => trigger.Cron("* * * * *")));
            });

        var fakeTime = (FakeTimeProvider)provider.GetRequiredService<TimeProvider>();
        var sut = provider.GetRequiredService<JobSchedulerBackgroundService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        await sut.SweepOnceAsync();
        fakeTime.Advance(TimeSpan.FromMinutes(1));
        await sut.SweepOnceAsync();

        var occurrences = await store.Queries.ListOccurrencesAsync();
        occurrences.Count.ShouldBeGreaterThanOrEqualTo(1);
        occurrences.ShouldAllBe(x => x.JobName == "cron");
        occurrences.ShouldAllBe(x => x.TriggerName == "every-minute");
        occurrences.ShouldAllBe(x => x.Status == JobOccurrenceStatus.Completed);
    }

    [Fact]
    public async Task SweepOnceAsync_WorkerPoolMaxConcurrencyIsRespected()
    {
        ConcurrencyTrackingJob.Reset();
        var dueUtc = new DateTimeOffset(2026, 05, 26, 09, 01, 00, TimeSpan.Zero);
        var provider = CreateProvider(
            options =>
            {
                options.EnableBackgroundExecution = false;
                options.MaxConcurrency = 2;
                options.BatchSize = 5;
            },
            services =>
            {
                services.AddJobScheduler().WithJob<ConcurrencyTrackingJob>("concurrent", job => job
                    .Description("Tracks concurrency.")
                    .AddTrigger("a", trigger => trigger.At(dueUtc))
                    .AddTrigger("b", trigger => trigger.At(dueUtc))
                    .AddTrigger("c", trigger => trigger.At(dueUtc)));
            });

        var fakeTime = (FakeTimeProvider)provider.GetRequiredService<TimeProvider>();
        var sut = provider.GetRequiredService<JobSchedulerBackgroundService>();

        await sut.SweepOnceAsync();
        fakeTime.Advance(TimeSpan.FromMinutes(1));
        var sweepTask = sut.SweepOnceAsync();
        await ConcurrencyTrackingJob.StartedCount.Task;
        ConcurrencyTrackingJob.MaxObserved.ShouldBeLessThanOrEqualTo(2);
        ConcurrencyTrackingJob.Release.SetResult(true);
        await sweepTask;
    }

    [Fact]
    public async Task SweepOnceAsync_JobConcurrencyLimit_IsRespected()
    {
        ConcurrencyTrackingJob.Reset(startSignalThreshold: 1);
        var dueUtc = new DateTimeOffset(2026, 05, 26, 09, 01, 00, TimeSpan.Zero);
        var provider = CreateProvider(
            options =>
            {
                options.EnableBackgroundExecution = false;
                options.MaxConcurrency = 3;
                options.BatchSize = 3;
            },
            services =>
            {
                services.AddJobScheduler().WithJob<ConcurrencyTrackingJob>("limited", job => job
                    .Description("Tracks per-job concurrency.")
                    .WithConcurrency(1)
                    .AddTrigger("a", trigger => trigger.At(dueUtc))
                    .AddTrigger("b", trigger => trigger.At(dueUtc))
                    .AddTrigger("c", trigger => trigger.At(dueUtc)));
            });

        var fakeTime = (FakeTimeProvider)provider.GetRequiredService<TimeProvider>();
        var sut = provider.GetRequiredService<JobSchedulerBackgroundService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        await sut.SweepOnceAsync();
        fakeTime.Advance(TimeSpan.FromMinutes(1));
        var sweepTask = sut.SweepOnceAsync();
        await ConcurrencyTrackingJob.StartedCount.Task;

        ConcurrencyTrackingJob.MaxObserved.ShouldBe(1);
        ConcurrencyTrackingJob.Release.SetResult(true);
        await sweepTask;

        var occurrences = await store.Queries.ListOccurrencesAsync();
        occurrences.Count(x => x.Status == JobOccurrenceStatus.Completed).ShouldBe(1);
        occurrences.Count(x => x.Status == JobOccurrenceStatus.Due).ShouldBe(2);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task SweepOnceAsync_DisabledOrPausedJobsAndTriggers_DoNotMaterializeWork(bool pauseJob, bool pauseTrigger)
    {
        var provider = CreateProvider(
            options => options.EnableBackgroundExecution = false,
            services =>
            {
                services.AddJobScheduler().WithJob<SuccessfulBackgroundJob>("scheduled", job => job
                    .Description("Scheduled work.")
                    .AddTrigger("scheduled-once", trigger => trigger.At(new DateTimeOffset(2026, 05, 26, 09, 01, 00, TimeSpan.Zero))));
            });

        var store = provider.GetRequiredService<IJobStoreProvider>();
        if (pauseJob)
        {
            await store.RuntimeStates.UpsertAsync(new JobRuntimeState { JobName = "scheduled", Paused = true, Enabled = true, CreatedDate = DateTimeOffset.UtcNow, UpdatedDate = DateTimeOffset.UtcNow });
        }

        if (pauseTrigger)
        {
            await store.TriggerRuntimeStates.UpsertAsync("scheduled", "scheduled-once", new JobTriggerRuntimeState(
                ActivatedUtc: null,
                DueUtc: null,
                LastMaterializedScheduledUtc: null,
                HasMaterializedOccurrence: false,
                Enabled: true,
                Paused: true,
                CreatedDate: DateTimeOffset.UtcNow,
                UpdatedDate: DateTimeOffset.UtcNow));
        }

        var sut = provider.GetRequiredService<JobSchedulerBackgroundService>();

        await sut.SweepOnceAsync();

        (await store.Queries.ListOccurrencesAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task StartAsync_StartupDelayTrigger_DoesNotBlockHostStartup()
    {
        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                this.ConfigureLogging(services);
                services.AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2026, 05, 26, 09, 00, 00, TimeSpan.Zero)));
                services.AddJobScheduler().WithBackgroundExecution(options =>
                {
                    options.StartupDelay = TimeSpan.FromDays(1);
                    options.SweepInterval = TimeSpan.FromDays(1);
                }).WithJob<SuccessfulBackgroundJob>("startup-delay", job => job
                    .Description("Startup delay.")
                    .AddTrigger("delayed", trigger => trigger.StartupDelay(TimeSpan.FromMinutes(5))));
            })
            .Build();

        var startTask = host.StartAsync();
        await Task.WhenAny(startTask, Task.Delay(TimeSpan.FromSeconds(1)));

        startTask.IsCompletedSuccessfully.ShouldBeTrue();
        await host.StopAsync();
    }

    [Fact]
    public async Task StopAsync_GracefulShutdown_StopsNewDispatchAndCancelsInFlightJob()
    {
        ShutdownAwareJob.Reset();
        var provider = CreateProvider(
            options =>
            {
                options.EnableBackgroundExecution = true;
                options.SweepInterval = TimeSpan.FromMilliseconds(50);
                options.BatchSize = 1;
                options.MaxConcurrency = 1;
            },
            services =>
            {
                services.AddJobScheduler().WithJob<ShutdownAwareJob>("shutdown", job => job
                    .Description("Observes shutdown.")
                    .AddTrigger("first", trigger => trigger.At(new DateTimeOffset(2026, 05, 26, 09, 01, 00, TimeSpan.Zero)))
                    .AddTrigger("second", trigger => trigger.At(new DateTimeOffset(2026, 05, 26, 09, 01, 00, TimeSpan.Zero))));
            });

        var fakeTime = (FakeTimeProvider)provider.GetRequiredService<TimeProvider>();
        var sut = provider.GetRequiredService<JobSchedulerBackgroundService>();
        var lifetime = (TestHostApplicationLifetime)provider.GetRequiredService<IHostApplicationLifetime>();

        await sut.StartAsync(CancellationToken.None);
        lifetime.NotifyStarted();
        fakeTime.Advance(TimeSpan.FromMinutes(1));
        await ShutdownAwareJob.FirstStart.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await sut.StopAsync(CancellationToken.None);
        sut.Dispose();

        ShutdownAwareJob.WasCancelled.ShouldBeTrue();
        ShutdownAwareJob.StartCount.ShouldBe(1);
    }

    [Fact]
    public async Task SweepOnceAsync_FailedOccurrence_IsRecoveredByNextSweep()
    {
        ResilientBackgroundJob.Reset();
        var dueUtc = new DateTimeOffset(2026, 05, 26, 09, 01, 00, TimeSpan.Zero);
        var provider = CreateProvider(
            options =>
            {
                options.EnableBackgroundExecution = false;
                options.MaxConcurrency = 1;
                options.BatchSize = 10;
            },
            services =>
            {
                services.AddJobScheduler().WithJob<ResilientBackgroundJob>("resilient", job => job
                    .Description("Continues sweeping after failures.")
                    .AddTrigger("fail", trigger => trigger.At(dueUtc).Priority(20))
                    .AddTrigger("succeed", trigger => trigger.At(dueUtc).Priority(10)));
            });

        var fakeTime = (FakeTimeProvider)provider.GetRequiredService<TimeProvider>();
        var sut = provider.GetRequiredService<JobSchedulerBackgroundService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        await sut.SweepOnceAsync();
        fakeTime.Advance(TimeSpan.FromMinutes(1));
        await sut.SweepOnceAsync();
        await sut.SweepOnceAsync();

        var occurrences = await store.Queries.ListOccurrencesAsync();

        ResilientBackgroundJob.AttemptedTriggers.ShouldBe(["fail", "succeed"]);
        occurrences.Count.ShouldBe(2);
        occurrences.Single(x => x.TriggerName == "fail").Status.ShouldBe(JobOccurrenceStatus.Failed);
        occurrences.Single(x => x.TriggerName == "succeed").Status.ShouldBe(JobOccurrenceStatus.Completed);
    }

    [Fact]
    public async Task SweepOnceAsync_IneligibleWorkerTarget_SkipsExecution()
    {
        OrderedBackgroundJob.ExecutionOrder.Clear();
        var dueUtc = new DateTimeOffset(2026, 05, 26, 09, 01, 00, TimeSpan.Zero);
        var provider = CreateProvider(
            options =>
            {
                options.EnableBackgroundExecution = false;
                options.MaxConcurrency = 1;
                options.BatchSize = 10;
                options.SchedulerInstanceId = "node-a";
            },
            services =>
            {
                services.AddJobScheduler().WithJob<OrderedBackgroundJob>("targeted", job => job
                    .Description("Runs only on another scheduler instance.")
                    .AddTrigger("manual", trigger => trigger
                        .At(dueUtc)
                        .TargetInstances("node-b")));
            });

        var fakeTime = (FakeTimeProvider)provider.GetRequiredService<TimeProvider>();
        var sut = provider.GetRequiredService<JobSchedulerBackgroundService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        await sut.SweepOnceAsync();
        fakeTime.Advance(TimeSpan.FromMinutes(1));
        await sut.SweepOnceAsync();

        OrderedBackgroundJob.ExecutionOrder.ShouldBeEmpty();
        var occurrence = (await store.Queries.ListOccurrencesAsync()).Single();
        occurrence.Status.ShouldBe(JobOccurrenceStatus.Due);
    }

    [Fact]
    public async Task StartAsync_UnhandledBackgroundFailure_InvokesRegisteredExceptionHandler()
    {
        var handler = new RecordingSchedulerExceptionHandler();
        var provider = CreateProvider(
            options =>
            {
                options.EnableBackgroundExecution = true;
                options.StartupDelay = TimeSpan.Zero;
                options.SweepInterval = TimeSpan.FromMinutes(1);
            },
            services =>
            {
                services.AddSingleton(handler);
                services.AddSingleton<IJobSchedulerExceptionHandler>(sp => sp.GetRequiredService<RecordingSchedulerExceptionHandler>());
                services.Replace(ServiceDescriptor.Singleton<IJobTriggerEvaluator, ThrowingTriggerEvaluator>());
                services.AddJobScheduler().WithJob<SuccessfulBackgroundJob>("scheduled", job => job
                    .Description("Scheduled work.")
                    .AddTrigger("scheduled-once", trigger => trigger.At(new DateTimeOffset(2026, 05, 26, 09, 01, 00, TimeSpan.Zero))));
            });

        var sut = provider.GetRequiredService<JobSchedulerBackgroundService>();
        var lifetime = (TestHostApplicationLifetime)provider.GetRequiredService<IHostApplicationLifetime>();

        await sut.StartAsync(CancellationToken.None);
        lifetime.NotifyStarted();
        var context = await handler.Invocation.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await sut.StopAsync(CancellationToken.None);

        context.Source.ShouldBe(JobSchedulerExceptionSource.BackgroundService);
        context.Definition.ShouldBeNull();
        context.Trigger.ShouldBeNull();
        context.OccurrenceId.ShouldBeNull();
        context.ExecutionId.ShouldBeNull();
        context.Exception.ShouldBeOfType<InvalidOperationException>();
        context.Exception.Message.ShouldBe("trigger evaluator boom");
    }

    private ServiceProvider CreateProvider(Action<JobSchedulerHostedOptions> configureOptions, Action<IServiceCollection> configureServices)
    {
        var services = new ServiceCollection();
        this.ConfigureLogging(services);
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2026, 05, 26, 09, 00, 00, TimeSpan.Zero)));
        services.TryAddSingleton<IHostApplicationLifetime, TestHostApplicationLifetime>();
        configureServices(services);

        var descriptor = services.LastOrDefault(x => x.ServiceType == typeof(JobSchedulerHostedOptions));
        var options = descriptor?.ImplementationInstance as JobSchedulerHostedOptions;
        if (options is null)
        {
            options = new JobSchedulerHostedOptions();
            services.Replace(ServiceDescriptor.Singleton(options));
        }

        configureOptions(options);
        return services.BuildServiceProvider();
    }

    private sealed class OrderedBackgroundJob : JobBase
    {
        public static List<string> ExecutionOrder { get; } = [];

        public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
        {
            lock (ExecutionOrder)
            {
                ExecutionOrder.Add(context.TriggerName);
            }

            return Task.FromResult(Result.Success());
        }
    }

    private sealed class SuccessfulBackgroundJob : JobBase
    {
        public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class ConcurrencyTrackingJob : JobBase
    {
        private static int current;

        public static int MaxObserved { get; private set; }

        public static int StartSignalThreshold { get; private set; } = 2;

        public static TaskCompletionSource<bool> StartedCount { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public static TaskCompletionSource<bool> Release { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public static void Reset(int startSignalThreshold = 2)
        {
            current = 0;
            MaxObserved = 0;
            StartSignalThreshold = startSignalThreshold;
            StartedCount = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public override async Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
        {
            var observed = Interlocked.Increment(ref current);
            MaxObserved = Math.Max(MaxObserved, observed);
            if (observed >= StartSignalThreshold)
            {
                StartedCount.TrySetResult(true);
            }

            await Release.Task.WaitAsync(cancellationToken);
            Interlocked.Decrement(ref current);
            return Result.Success();
        }
    }

    private sealed class ShutdownAwareJob : JobBase
    {
        public static int StartCount;
        public static bool WasCancelled;
        public static TaskCompletionSource<bool> FirstStart { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public static void Reset()
        {
            StartCount = 0;
            WasCancelled = false;
            FirstStart = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public override async Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref StartCount);
            FirstStart.TrySetResult(true);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, context.CancellationToken);
                return Result.Success();
            }
            catch (OperationCanceledException)
            {
                WasCancelled = true;
                throw;
            }
        }
    }

    private sealed class ResilientBackgroundJob : JobBase
    {
        public static List<string> AttemptedTriggers { get; } = [];

        public static void Reset()
        {
            AttemptedTriggers.Clear();
        }

        public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
        {
            lock (AttemptedTriggers)
            {
                AttemptedTriggers.Add(context.TriggerName);
            }

            return context.TriggerName == "fail"
                ? Task.FromResult(Result.Failure().WithError(new ValidationError("planned background failure")))
                : Task.FromResult(Result.Success());
        }
    }

    private sealed class ThrowingTriggerEvaluator : IJobTriggerEvaluator
    {
        public Result<JobTriggerEvaluationResult> Materialize(JobDefinition job, JobTriggerDefinition trigger, JobTriggerEvaluationRequest request)
            => throw new InvalidOperationException("trigger evaluator boom");
    }

    private sealed class RecordingSchedulerExceptionHandler : IJobSchedulerExceptionHandler
    {
        public TaskCompletionSource<JobSchedulerExceptionContext> Invocation { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task HandleAsync(JobSchedulerExceptionContext context, CancellationToken cancellationToken = default)
        {
            this.Invocation.TrySetResult(context);
            return Task.CompletedTask;
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

        public void NotifyStarted()
        {
            if (!this.started.IsCancellationRequested)
            {
                this.started.Cancel();
            }
        }
    }
}
