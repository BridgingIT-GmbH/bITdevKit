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

public class JobSchedulerLeaseTests(ITestOutputHelper output) : JobSchedulerTestBase(output)
{
    [Fact]
    public async Task LeaseStore_OnlyOneWorkerCanAcquireOccurrenceLease()
    {
        var provider = CreateProvider();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        var first = await store.Leases.TryAcquireAsync(Guid.NewGuid(), "scheduler-a", TimeSpan.FromMinutes(1));
        var second = await store.Leases.TryAcquireAsync(first.OccurrenceId, "scheduler-b", TimeSpan.FromMinutes(1));

        first.ShouldNotBeNull();
        second.ShouldBeNull();
    }

    [Fact]
    public async Task LeaseStore_ConcurrentAcquisitionAttempts_AreRejected()
    {
        var provider = CreateProvider();
        var store = provider.GetRequiredService<IJobStoreProvider>();
        var occurrenceId = Guid.NewGuid();

        var attempts = await Task.WhenAll(
            store.Leases.TryAcquireAsync(occurrenceId, "scheduler-a", TimeSpan.FromMinutes(1)),
            store.Leases.TryAcquireAsync(occurrenceId, "scheduler-b", TimeSpan.FromMinutes(1)));

        attempts.Count(x => x is not null).ShouldBe(1);
    }

    [Fact]
    public async Task LeaseStore_Renewal_ExtendsOwnership()
    {
        var provider = CreateProvider();
        var store = provider.GetRequiredService<IJobStoreProvider>();
        var fakeTime = (FakeTimeProvider)provider.GetRequiredService<TimeProvider>();
        var occurrenceId = Guid.NewGuid();

        var lease = await store.Leases.TryAcquireAsync(occurrenceId, "scheduler-a", TimeSpan.FromMinutes(1));
        fakeTime.Advance(TimeSpan.FromSeconds(30));
        var renewed = await store.Leases.RenewAsync(occurrenceId, "scheduler-a", lease.OwnershipToken, TimeSpan.FromMinutes(1));

        renewed.ShouldNotBeNull();
        renewed.ExpiresUtc.ShouldBe(fakeTime.GetUtcNow().AddMinutes(1));
        renewed.RenewalCount.ShouldBe(1);
    }

    [Fact]
    public async Task SweepOnceAsync_ExpiredLeasesBecomeRecoverable()
    {
        SuccessfulLeaseJob.RunCount = 0;
        var provider = CreateProvider(options =>
        {
            options.EnableBackgroundExecution = false;
            options.LeaseDuration = TimeSpan.FromSeconds(5);
            options.LeaseRenewalInterval = TimeSpan.Zero;
        }, services =>
        {
            services.AddJobScheduler().WithJob<SuccessfulLeaseJob>("lease-job", job => job
                .Description("Recoverable job.")
                .AddTrigger("manual", trigger => trigger.Manual()));
        });

        var fakeTime = (FakeTimeProvider)provider.GetRequiredService<TimeProvider>();
        var store = provider.GetRequiredService<IJobStoreProvider>();
        var scheduler = provider.GetRequiredService<JobSchedulerService>();
        var background = provider.GetRequiredService<JobSchedulerBackgroundService>();

        var occurrenceId = await CreateDueOccurrenceAsync(store, "lease-job", "manual", fakeTime.GetUtcNow());
        var lease = await store.Leases.TryAcquireAsync(occurrenceId, "abandoned-scheduler", TimeSpan.FromSeconds(5));
        await store.Occurrences.UpdateAsync((await store.Occurrences.GetAsync(occurrenceId)) with { Status = JobOccurrenceStatus.Running, UpdatedDate = fakeTime.GetUtcNow() });

        fakeTime.Advance(TimeSpan.FromSeconds(6));
        await background.SweepOnceAsync();

        var recovered = await store.Occurrences.GetAsync(occurrenceId);
        recovered.Status.ShouldBe(JobOccurrenceStatus.Completed);
        SuccessfulLeaseJob.RunCount.ShouldBe(1);
        (await store.Leases.GetAsync(occurrenceId)).ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteStoredOccurrenceAsync_WorkerCannotFinalizeAfterLeaseLoss()
    {
        LeaseLossAwareJob.Reset();
        var provider = CreateProvider(options =>
        {
            options.EnableBackgroundExecution = false;
            options.LeaseDuration = TimeSpan.FromSeconds(1);
            options.LeaseRenewalInterval = TimeSpan.Zero;
        }, services =>
        {
            services.AddJobScheduler().WithJob<LeaseLossAwareJob>("lease-job", job => job
                .Description("Loses lease before finalization.")
                .AddTrigger("manual", trigger => trigger.Manual()));
        });

        var fakeTime = (FakeTimeProvider)provider.GetRequiredService<TimeProvider>();
        var store = provider.GetRequiredService<IJobStoreProvider>();
        var scheduler = provider.GetRequiredService<JobSchedulerService>();
        var occurrenceId = await CreateDueOccurrenceAsync(store, "lease-job", "manual", fakeTime.GetUtcNow());

        var executionTask = scheduler.ExecuteStoredOccurrenceAsync(occurrenceId);
        await LeaseLossAwareJob.Started.Task;
        fakeTime.Advance(TimeSpan.FromSeconds(2));
        LeaseLossAwareJob.Release.TrySetResult(true);
        var result = await executionTask;

        result.IsFailure.ShouldBeTrue();
        (await store.Occurrences.GetAsync(occurrenceId)).Status.ShouldBe(JobOccurrenceStatus.Running);
        (await store.Executions.ListByOccurrenceAsync(occurrenceId)).Single().Status.ShouldBe(JobExecutionStatus.Started);
    }

    [Fact]
    public async Task SweepOnceAsync_RecoveredOccurrenceReturnsToRunnableState()
    {
        SuccessfulLeaseJob.RunCount = 0;
        var provider = CreateProvider(options =>
        {
            options.EnableBackgroundExecution = false;
            options.LeaseDuration = TimeSpan.FromSeconds(2);
            options.LeaseRenewalInterval = TimeSpan.Zero;
        }, services =>
        {
            services.AddJobScheduler().WithJob<SuccessfulLeaseJob>("lease-job", job => job
                .Description("Recovered occurrence.")
                .AddTrigger("manual", trigger => trigger.Manual()));
        });

        var fakeTime = (FakeTimeProvider)provider.GetRequiredService<TimeProvider>();
        var store = provider.GetRequiredService<IJobStoreProvider>();
        var background = provider.GetRequiredService<JobSchedulerBackgroundService>();
        var occurrenceId = await CreateDueOccurrenceAsync(store, "lease-job", "manual", fakeTime.GetUtcNow());

        await store.Leases.TryAcquireAsync(occurrenceId, "abandoned-scheduler", TimeSpan.FromSeconds(2));
        await store.Occurrences.UpdateAsync((await store.Occurrences.GetAsync(occurrenceId)) with { Status = JobOccurrenceStatus.Running, UpdatedDate = fakeTime.GetUtcNow() });

        fakeTime.Advance(TimeSpan.FromSeconds(3));
        await background.SweepOnceAsync();

        SuccessfulLeaseJob.RunCount.ShouldBe(1);
        (await store.Occurrences.GetAsync(occurrenceId)).Status.ShouldBe(JobOccurrenceStatus.Completed);
        (await store.Executions.ListByOccurrenceAsync(occurrenceId)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task StopAsync_ShutdownReleasesLeasesThroughCancellationFinalization()
    {
        ShutdownLeaseJob.Reset();
        var dueUtc = new DateTimeOffset(2026, 05, 26, 09, 01, 00, TimeSpan.Zero);
        var provider = CreateProvider(options =>
        {
            options.EnableBackgroundExecution = true;
            options.SweepInterval = TimeSpan.FromMilliseconds(50);
            options.BatchSize = 1;
            options.MaxConcurrency = 1;
            options.LeaseDuration = TimeSpan.FromMinutes(1);
            options.LeaseRenewalInterval = TimeSpan.FromSeconds(5);
        }, services =>
        {
            services.AddJobScheduler().WithJob<ShutdownLeaseJob>("shutdown-job", job => job
                .Description("Releases lease on shutdown.")
                .AddTrigger("first", trigger => trigger.At(dueUtc)));
        });

        var fakeTime = (FakeTimeProvider)provider.GetRequiredService<TimeProvider>();
        var store = provider.GetRequiredService<IJobStoreProvider>();
        var sut = provider.GetRequiredService<JobSchedulerBackgroundService>();
        var lifetime = (TestHostApplicationLifetime)provider.GetRequiredService<IHostApplicationLifetime>();

        await sut.StartAsync(CancellationToken.None);
        lifetime.NotifyStarted();
        fakeTime.Advance(TimeSpan.FromMinutes(1));
        await ShutdownLeaseJob.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        (await store.Leases.ListAsync()).Count.ShouldBe(1);
        await sut.StopAsync(CancellationToken.None);
        sut.Dispose();

        (await store.Leases.ListAsync()).ShouldBeEmpty();
    }

    private ServiceProvider CreateProvider(Action<JobSchedulerHostedOptions> configureOptions = null, Action<IServiceCollection> configureServices = null)
    {
        var services = new ServiceCollection();
        this.ConfigureLogging(services);
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2026, 05, 26, 09, 00, 00, TimeSpan.Zero)));
        services.TryAddSingleton<IHostApplicationLifetime, TestHostApplicationLifetime>();
        services.AddJobScheduler();
        configureServices?.Invoke(services);

        var descriptor = services.LastOrDefault(x => x.ServiceType == typeof(JobSchedulerHostedOptions));
        var options = descriptor?.ImplementationInstance as JobSchedulerHostedOptions;
        if (options is null)
        {
            options = new JobSchedulerHostedOptions();
            services.Replace(ServiceDescriptor.Singleton(options));
        }

        configureOptions?.Invoke(options);
        return services.BuildServiceProvider();
    }

    private static async Task<Guid> CreateDueOccurrenceAsync(IJobStoreProvider store, string jobName, string triggerName, DateTimeOffset dueUtc)
    {
        var occurrence = new JobOccurrence
        {
            OccurrenceId = Guid.NewGuid(),
            OccurrenceKey = $"{jobName}:{triggerName}:{Guid.NewGuid():N}",
            JobName = jobName,
            TriggerName = triggerName,
            TriggerType = JobTriggerType.Manual,
            Status = JobOccurrenceStatus.Due,
            DueUtc = dueUtc,
            Data = Unit.Value,
            DataType = typeof(Unit),
            CreatedDate = dueUtc,
            UpdatedDate = dueUtc,
        };

        (await store.Occurrences.TryCreateAsync(occurrence)).ShouldBeTrue();
        return occurrence.OccurrenceId;
    }

    private sealed class SuccessfulLeaseJob : JobBase
    {
        public static int RunCount;

        public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref RunCount);
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class LeaseLossAwareJob : JobBase
    {
        public static TaskCompletionSource<bool> Started { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public static TaskCompletionSource<bool> Release { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public static void Reset()
        {
            Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public override async Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult(true);
            await Release.Task.WaitAsync(CancellationToken.None);
            return Result.Success();
        }
    }

    private sealed class ShutdownLeaseJob : JobBase
    {
        public static TaskCompletionSource<bool> Started { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public static void Reset()
        {
            Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public override async Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Result.Success();
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
