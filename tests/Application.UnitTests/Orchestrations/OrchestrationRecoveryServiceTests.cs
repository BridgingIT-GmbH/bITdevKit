// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Orchestrations;

using System.Collections.Concurrent;
using System.Reflection;
using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class OrchestrationRecoveryServiceTests(ITestOutputHelper output) : OrchestrationTestBase(output)
{
    [Fact]
    public async Task RepairWaitingInstanceAsync_WhenDelayWaitCrashesAfterWaitingSnapshot_PersistsMissingTimerWithoutReplayingActivity()
    {
        var clock = new FakeOrchestrationClock();
        var provider = new WaitBoundaryFaultingPersistenceProvider(FaultPoint.WaitingSnapshotSaved);
        using var serviceProvider = CreateServices(
            services => services.AddOrchestrations().WithOrchestration<DelayWaitOrchestration>(),
            clock,
            provider);

        var instanceId = await CreateInstanceAsync<DelayWaitOrchestration, RecoveryData>(serviceProvider, new RecoveryData());
        await ContinueInstanceAsync(serviceProvider, instanceId);

        var queries = serviceProvider.GetRequiredService<IOrchestrationQueryStore>();
        var executor = serviceProvider.GetRequiredService<InMemoryOrchestrationExecutor>();
        var worker = serviceProvider.GetRequiredService<OrchestrationRecoveryService>();

        var waiting = await queries.GetContextAsync<RecoveryData>(instanceId, serviceProvider);
        var timersBeforeRepair = await queries.GetTimersAsync(instanceId);

        waiting.Status.ShouldBe(OrchestrationStatus.Waiting);
        waiting.Data.DelayExecutions.ShouldBe(1);
        timersBeforeRepair.ShouldBeEmpty();

        var repair = await executor.RepairWaitingInstanceAsync(instanceId);
        repair.Result.ShouldBe(InMemoryOrchestrationExecutor.RecoveryActionResult.Repaired);
        repair.AffectedTimerCount.ShouldBe(1);

        var timersAfterRepair = await queries.GetTimersAsync(instanceId);
        timersAfterRepair.Count.ShouldBe(1);
        timersAfterRepair.Single().Status.ShouldBe(OrchestrationTimerStatus.Pending);

        clock.Advance(TimeSpan.FromMilliseconds(150));
        await worker.SweepOnceAsync();

        var completed = await WaitForInstanceAsync(queries, instanceId, item => item.Status == OrchestrationStatus.Completed);
        var context = await queries.GetContextAsync<RecoveryData>(instanceId, serviceProvider);

        completed.Status.ShouldBe(OrchestrationStatus.Completed);
        context.Data.DelayExecutions.ShouldBe(1);
    }

    [Fact]
    public async Task SweepOnceAsync_WhenDelayWaitCrashesAfterTimerScheduling_ReusesExistingTimerWithoutReplayingActivity()
    {
        var clock = new FakeOrchestrationClock();
        var provider = new WaitBoundaryFaultingPersistenceProvider(FaultPoint.TimerScheduledHistoryAppended);
        using var serviceProvider = CreateServices(
            services => services.AddOrchestrations().WithOrchestration<DelayWaitOrchestration>(),
            clock,
            provider);

        var instanceId = await CreateInstanceAsync<DelayWaitOrchestration, RecoveryData>(serviceProvider, new RecoveryData());
        await ContinueInstanceAsync(serviceProvider, instanceId);

        var queries = serviceProvider.GetRequiredService<IOrchestrationQueryStore>();
        var worker = serviceProvider.GetRequiredService<OrchestrationRecoveryService>();

        var waiting = await queries.GetContextAsync<RecoveryData>(instanceId, serviceProvider);
        var timers = await queries.GetTimersAsync(instanceId);

        waiting.Status.ShouldBe(OrchestrationStatus.Waiting);
        waiting.Data.DelayExecutions.ShouldBe(1);
        timers.Count.ShouldBe(1);

        clock.Advance(TimeSpan.FromMilliseconds(150));
        await worker.SweepOnceAsync();

        var completed = await WaitForInstanceAsync(queries, instanceId, item => item.Status == OrchestrationStatus.Completed);
        var context = await queries.GetContextAsync<RecoveryData>(instanceId, serviceProvider);
        var timersAfterCompletion = await queries.GetTimersAsync(instanceId);

        completed.Status.ShouldBe(OrchestrationStatus.Completed);
        context.Data.DelayExecutions.ShouldBe(1);
        timersAfterCompletion.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RepairWaitingInstanceAsync_WhenStateTimeoutCrashesAfterWaitingSnapshot_RecreatesMissingTimerAndCompletes()
    {
        var clock = new FakeOrchestrationClock();
        var provider = new WaitBoundaryFaultingPersistenceProvider(FaultPoint.WaitingSnapshotSaved);
        using var serviceProvider = CreateServices(
            services => services.AddOrchestrations().WithOrchestration<StateTimeoutOrchestration>(),
            clock,
            provider);

        var instanceId = await CreateInstanceAsync<StateTimeoutOrchestration, RecoveryData>(serviceProvider, new RecoveryData());
        await ContinueInstanceAsync(serviceProvider, instanceId);

        var queries = serviceProvider.GetRequiredService<IOrchestrationQueryStore>();
        var worker = serviceProvider.GetRequiredService<OrchestrationRecoveryService>();

        (await queries.GetTimersAsync(instanceId)).ShouldBeEmpty();

        await worker.SweepOnceAsync();
        (await queries.GetTimersAsync(instanceId)).Count.ShouldBe(1);

        clock.Advance(TimeSpan.FromMilliseconds(150));
        await worker.SweepOnceAsync();

        var completed = await WaitForInstanceAsync(queries, instanceId, item => item.Status == OrchestrationStatus.Completed);
        var context = await queries.GetContextAsync<RecoveryData>(instanceId, serviceProvider);

        completed.CurrentState.ShouldBe("Expired");
        context.Data.TimeoutExecutions.ShouldBe(1);
    }

    [Fact]
    public async Task SweepOnceAsync_WhenWaitingForSignalsOnly_DoesNotCreateRecoveryTimers()
    {
        var clock = new FakeOrchestrationClock();
        using var serviceProvider = CreateServices(
            services => services.AddOrchestrations().WithOrchestration<SignalOnlyWaitOrchestration>(),
            clock);

        var instanceId = await CreateInstanceAsync<SignalOnlyWaitOrchestration, RecoveryData>(serviceProvider, new RecoveryData());
        await ContinueInstanceAsync(serviceProvider, instanceId);

        var worker = serviceProvider.GetRequiredService<OrchestrationRecoveryService>();
        var queries = serviceProvider.GetRequiredService<IOrchestrationQueryStore>();

        await worker.SweepOnceAsync();

        var waiting = await queries.GetInstanceAsync(instanceId);
        var timers = await queries.GetTimersAsync(instanceId);

        waiting.Status.ShouldBe(OrchestrationStatus.Waiting);
        timers.ShouldBeEmpty();
    }

    [Fact]
    public async Task SweepOnceAsync_WhenTimerIsAlreadyDueAtStartup_CompletesWaitingInstance()
    {
        var clock = new FakeOrchestrationClock();
        using var serviceProvider = CreateServices(
            services => services.AddOrchestrations().WithOrchestration<StateTimeoutOrchestration>(),
            clock);

        var instanceId = await CreateInstanceAsync<StateTimeoutOrchestration, RecoveryData>(serviceProvider, new RecoveryData());
        await ContinueInstanceAsync(serviceProvider, instanceId);

        var worker = serviceProvider.GetRequiredService<OrchestrationRecoveryService>();
        var queries = serviceProvider.GetRequiredService<IOrchestrationQueryStore>();

        clock.Advance(TimeSpan.FromMilliseconds(150));
        await worker.SweepOnceAsync();

        var completed = await WaitForInstanceAsync(queries, instanceId, item => item.Status == OrchestrationStatus.Completed);
        completed.CurrentState.ShouldBe("Expired");
    }

    [Fact]
    public async Task SweepOnceAsync_WhenTimerBecomesDueLater_CompletesOnSubsequentSweep()
    {
        var clock = new FakeOrchestrationClock();
        using var serviceProvider = CreateServices(
            services => services.AddOrchestrations().WithOrchestration<StateTimeoutOrchestration>(),
            clock);

        var instanceId = await CreateInstanceAsync<StateTimeoutOrchestration, RecoveryData>(serviceProvider, new RecoveryData());
        await ContinueInstanceAsync(serviceProvider, instanceId);

        var worker = serviceProvider.GetRequiredService<OrchestrationRecoveryService>();
        var queries = serviceProvider.GetRequiredService<IOrchestrationQueryStore>();

        await worker.SweepOnceAsync();
        (await queries.GetInstanceAsync(instanceId)).Status.ShouldBe(OrchestrationStatus.Waiting);

        clock.Advance(TimeSpan.FromMilliseconds(150));
        await worker.SweepOnceAsync();

        (await queries.GetInstanceAsync(instanceId)).Status.ShouldBe(OrchestrationStatus.Completed);
    }

    [Fact]
    public async Task SweepOnceAsync_WhenWorkersRaceForDueTimer_ProcessesInstanceOnlyOnce()
    {
        var clock = new FakeOrchestrationClock();
        using var serviceProvider = CreateServices(
            services =>
            {
                services.AddSingleton(new RecoveryProbe());
                services.AddOrchestrations().WithOrchestration<SlowTimeoutOrchestration>();
            },
            clock);

        var instanceId = await CreateInstanceAsync<SlowTimeoutOrchestration, RecoveryData>(serviceProvider, new RecoveryData());
        await ContinueInstanceAsync(serviceProvider, instanceId);

        var queries = serviceProvider.GetRequiredService<IOrchestrationQueryStore>();
        var worker = serviceProvider.GetRequiredService<OrchestrationRecoveryService>();
        var probe = serviceProvider.GetRequiredService<RecoveryProbe>();

        clock.Advance(TimeSpan.FromMilliseconds(150));
        await Task.WhenAll(worker.SweepOnceAsync(), worker.SweepOnceAsync());
        await WaitForInstanceAsync(queries, instanceId, item => item.Status == OrchestrationStatus.Completed);

        probe.TimeoutExecutions.ShouldBe(1);
    }

    [Fact]
    public async Task SweepOnceAsync_WhenMultipleDueInstancesExist_CompletesAllInstancesInSingleSweep()
    {
        var clock = new FakeOrchestrationClock();
        using var serviceProvider = CreateServices(
            services => services.AddOrchestrations().WithOrchestration<StateTimeoutOrchestration>(),
            clock);

        var firstInstanceId = await CreateInstanceAsync<StateTimeoutOrchestration, RecoveryData>(serviceProvider, new RecoveryData());
        var secondInstanceId = await CreateInstanceAsync<StateTimeoutOrchestration, RecoveryData>(serviceProvider, new RecoveryData());
        var thirdInstanceId = await CreateInstanceAsync<StateTimeoutOrchestration, RecoveryData>(serviceProvider, new RecoveryData());

        await ContinueInstanceAsync(serviceProvider, firstInstanceId);
        await ContinueInstanceAsync(serviceProvider, secondInstanceId);
        await ContinueInstanceAsync(serviceProvider, thirdInstanceId);

        var queries = serviceProvider.GetRequiredService<IOrchestrationQueryStore>();
        var worker = serviceProvider.GetRequiredService<OrchestrationRecoveryService>();

        clock.Advance(TimeSpan.FromMilliseconds(150));
        await worker.SweepOnceAsync();

        var firstCompleted = await WaitForInstanceAsync(queries, firstInstanceId, item => item.Status == OrchestrationStatus.Completed);
        var secondCompleted = await WaitForInstanceAsync(queries, secondInstanceId, item => item.Status == OrchestrationStatus.Completed);
        var thirdCompleted = await WaitForInstanceAsync(queries, thirdInstanceId, item => item.Status == OrchestrationStatus.Completed);
        var firstContext = await queries.GetContextAsync<RecoveryData>(firstInstanceId, serviceProvider);
        var secondContext = await queries.GetContextAsync<RecoveryData>(secondInstanceId, serviceProvider);
        var thirdContext = await queries.GetContextAsync<RecoveryData>(thirdInstanceId, serviceProvider);

        firstCompleted.CurrentState.ShouldBe("Expired");
        secondCompleted.CurrentState.ShouldBe("Expired");
        thirdCompleted.CurrentState.ShouldBe("Expired");
        firstContext.Data.TimeoutExecutions.ShouldBe(1);
        secondContext.Data.TimeoutExecutions.ShouldBe(1);
        thirdContext.Data.TimeoutExecutions.ShouldBe(1);
    }

    [Fact]
    public async Task SweepOnceAsync_WhenOneDueInstanceHasActiveLease_ContinuesOtherDueInstancesAndRetriesSkippedInstanceLater()
    {
        var clock = new FakeOrchestrationClock();
        using var serviceProvider = CreateServices(
            services => services.AddOrchestrations().WithOrchestration<StateTimeoutOrchestration>(),
            clock);

        var leasedInstanceId = await CreateInstanceAsync<StateTimeoutOrchestration, RecoveryData>(serviceProvider, new RecoveryData());
        var freeInstanceId = await CreateInstanceAsync<StateTimeoutOrchestration, RecoveryData>(serviceProvider, new RecoveryData());

        await ContinueInstanceAsync(serviceProvider, leasedInstanceId);
        await ContinueInstanceAsync(serviceProvider, freeInstanceId);

        var leases = serviceProvider.GetRequiredService<IOrchestrationLeaseStore>();
        var queries = serviceProvider.GetRequiredService<IOrchestrationQueryStore>();
        var worker = serviceProvider.GetRequiredService<OrchestrationRecoveryService>();

        var heldLease = await leases.AcquireAsync(leasedInstanceId, "held-worker", TimeSpan.FromMinutes(1));

        clock.Advance(TimeSpan.FromMilliseconds(150));
        await worker.SweepOnceAsync();

        (await queries.GetInstanceAsync(leasedInstanceId)).Status.ShouldBe(OrchestrationStatus.Waiting);
        (await WaitForInstanceAsync(queries, freeInstanceId, item => item.Status == OrchestrationStatus.Completed)).CurrentState.ShouldBe("Expired");

        await leases.ReleaseAsync(leasedInstanceId, heldLease.LeaseId, heldLease.Owner);
        await worker.SweepOnceAsync();

        (await WaitForInstanceAsync(queries, leasedInstanceId, item => item.Status == OrchestrationStatus.Completed)).CurrentState.ShouldBe("Expired");
    }

    [Fact]
    public async Task HostedService_WhenBackgroundExecutionIsDisabled_DoesNotAdvanceInstancesAutomatically()
    {
        var clock = new FakeOrchestrationClock();
        var settings = new OrchestrationExecutionSettings
        {
            EnableBackgroundExecution = false,
            StartupDelay = TimeSpan.Zero,
            BackgroundSweepInterval = TimeSpan.FromMilliseconds(20),
        };

        using var serviceProvider = CreateServices(
            services => services.AddOrchestrations().WithOrchestration<StateTimeoutOrchestration>(),
            clock,
            settings: settings);

        var instanceId = await CreateInstanceAsync<StateTimeoutOrchestration, RecoveryData>(serviceProvider, new RecoveryData());
        await ContinueInstanceAsync(serviceProvider, instanceId);

        var hostedService = serviceProvider.GetRequiredService<OrchestrationRecoveryService>();
        var queries = serviceProvider.GetRequiredService<IOrchestrationQueryStore>();

        await hostedService.StartAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromMilliseconds(150));
        await Task.Delay(50);

        (await queries.GetInstanceAsync(instanceId)).Status.ShouldBe(OrchestrationStatus.Waiting);

        await hostedService.SweepOnceAsync();
        (await WaitForInstanceAsync(queries, instanceId, item => item.Status == OrchestrationStatus.Completed)).Status.ShouldBe(OrchestrationStatus.Completed);

        await hostedService.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_WhenApplicationHasNotStarted_DoesNotSweepUntilHostSignalsStarted()
    {
        var clock = new FakeOrchestrationClock();
        var settings = new OrchestrationExecutionSettings
        {
            EnableBackgroundExecution = true,
            StartupDelay = TimeSpan.Zero,
            BackgroundSweepInterval = TimeSpan.FromMilliseconds(20),
        };
        var provider = new WaitBoundaryFaultingPersistenceProvider(FaultPoint.WaitingSnapshotSaved);

        using var serviceProvider = CreateServices(
            services => services.AddOrchestrations().WithOrchestration<DelayWaitOrchestration>(),
            clock,
            provider,
            settings: settings);

        var instanceId = await CreateInstanceAsync<DelayWaitOrchestration, RecoveryData>(serviceProvider, new RecoveryData());
        await ContinueInstanceAsync(serviceProvider, instanceId);

        var hostedService = serviceProvider.GetRequiredService<OrchestrationRecoveryService>();
        var queries = serviceProvider.GetRequiredService<IOrchestrationQueryStore>();
        var applicationLifetime = (TestHostApplicationLifetime)serviceProvider.GetRequiredService<IHostApplicationLifetime>();

        clock.Advance(TimeSpan.FromMilliseconds(150));
        await hostedService.StartAsync(CancellationToken.None);
        await Task.Delay(50);

        (await queries.GetInstanceAsync(instanceId)).Status.ShouldBe(OrchestrationStatus.Waiting);
        (await queries.GetTimersAsync(instanceId)).ShouldBeEmpty();

        applicationLifetime.NotifyStarted();
        await WaitForInstanceAsync(queries, instanceId, item => item.Status == OrchestrationStatus.Completed);

        await hostedService.StopAsync(CancellationToken.None);
    }

    private ServiceProvider CreateServices(
        Action<IServiceCollection> configure,
        FakeOrchestrationClock clock = null,
        IOrchestrationStorageProvider persistenceProvider = null,
        OrchestrationExecutionSettings settings = null)
    {
        var services = new ServiceCollection();
        this.ConfigureLogging(services);
        services.AddSingleton<IHostApplicationLifetime, TestHostApplicationLifetime>();
        services.AddSingleton(settings ?? new OrchestrationExecutionSettings { EnableBackgroundExecution = false });
        services.AddSingleton<IOrchestrationClock>(clock ?? new FakeOrchestrationClock());
        if (persistenceProvider is not null)
        {
            services.AddSingleton(typeof(IOrchestrationStorageProvider), persistenceProvider);
        }

        configure(services);
        return services.BuildServiceProvider();
    }

    private static async Task<Guid> CreateInstanceAsync<TOrchestration, TData>(ServiceProvider serviceProvider, TData data)
        where TOrchestration : class, IOrchestration<TData>
        where TData : class, IOrchestrationData
    {
        var executor = serviceProvider.GetRequiredService<InMemoryOrchestrationExecutor>();
        var method = typeof(InMemoryOrchestrationExecutor).GetMethod("CreateInstanceAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("CreateInstanceAsync could not be resolved.");

        var task = (Task<Guid>)method.MakeGenericMethod(typeof(TOrchestration), typeof(TData))
            .Invoke(executor, [data, CancellationToken.None])!;

        return await task.ConfigureAwait(false);
    }

    private static async Task ContinueInstanceAsync(ServiceProvider serviceProvider, Guid instanceId)
    {
        var executor = serviceProvider.GetRequiredService<InMemoryOrchestrationExecutor>();
        var method = typeof(InMemoryOrchestrationExecutor).GetMethod("ContinueInstanceAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ContinueInstanceAsync could not be resolved.");

        var task = (Task)method.Invoke(executor, [instanceId, CancellationToken.None])!;
        await task.ConfigureAwait(false);
    }

    private static async Task<OrchestrationInstanceSnapshot> WaitForInstanceAsync(
        IOrchestrationQueryStore queries,
        Guid instanceId,
        Func<OrchestrationInstanceSnapshot, bool> predicate,
        TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout ?? TimeSpan.FromSeconds(5));

        while (DateTimeOffset.UtcNow < deadline)
        {
            var instance = await queries.GetInstanceAsync(instanceId).ConfigureAwait(false);
            if (instance is not null && predicate(instance))
            {
                return instance;
            }

            await Task.Delay(25).ConfigureAwait(false);
        }

        throw new TimeoutException($"Orchestration instance '{instanceId}' did not reach the expected condition in time.");
    }

    private sealed class RecoveryData : IOrchestrationData
    {
        public int DelayExecutions { get; set; }

        public int TimeoutExecutions { get; set; }
    }

    private sealed class RecoveryProbe
    {
        public int TimeoutExecutions;
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
            this.stopping.Cancel();
            this.stopped.Cancel();
        }

        public void NotifyStarted()
        {
            this.started.Cancel();
        }
    }

    private sealed class DelayWaitOrchestration : Orchestration<RecoveryData>
    {
        protected override void Define(IOrchestrationBuilder<RecoveryData> builder)
        {
            builder
                .State("Waiting", state => state
                    .Activity((context, cancellationToken) =>
                    {
                        context.Data.DelayExecutions++;
                        return Task.FromResult(OrchestrationOutcome.Wait(TimeSpan.FromMilliseconds(100), "delay"));
                    }, "DelayStep")
                    .TransitionTo("Done"))
                .State("Done", state => state.Complete());
        }
    }

    private sealed class StateTimeoutOrchestration : Orchestration<RecoveryData>
    {
        protected override void Define(IOrchestrationBuilder<RecoveryData> builder)
        {
            builder
                .State("AwaitingTimeout", state => state
                    .TimeoutAfter(TimeSpan.FromMilliseconds(100))
                    .TransitionTo("Expired"))
                .State("Expired", state => state
                    .Activity((context, cancellationToken) =>
                    {
                        context.Data.TimeoutExecutions++;
                        return Task.FromResult(OrchestrationOutcome.Continue());
                    }, "ExpireStep")
                    .Complete());
        }
    }

    private sealed class SignalOnlyWaitOrchestration : Orchestration<RecoveryData>
    {
        protected override void Define(IOrchestrationBuilder<RecoveryData> builder)
        {
            builder
                .State("AwaitingSignal", state => state
                    .WaitForSignal("Continue", signal => signal.TransitionTo("Done")))
                .State("Done", state => state.Complete());
        }
    }

    private sealed class SlowTimeoutOrchestration : Orchestration<RecoveryData>
    {
        protected override void Define(IOrchestrationBuilder<RecoveryData> builder)
        {
            builder
                .State("AwaitingTimeout", state => state
                    .TimeoutAfter(TimeSpan.FromMilliseconds(100))
                    .TransitionTo("Expired"))
                .State("Expired", state => state
                    .Activity(async (context, cancellationToken) =>
                    {
                        await Task.Delay(100, cancellationToken);
                        Interlocked.Increment(ref context.Services.GetRequiredService<RecoveryProbe>().TimeoutExecutions);
                        return OrchestrationOutcome.Continue();
                    }, "SlowExpire")
                    .Complete());
        }
    }

    private enum FaultPoint
    {
        WaitingSnapshotSaved,
        TimerScheduledHistoryAppended,
    }

    private sealed class WaitBoundaryFaultingPersistenceProvider : IOrchestrationStorageProvider
    {
        private readonly InMemoryOrchestrationStorageProvider inner;

        public WaitBoundaryFaultingPersistenceProvider(FaultPoint faultPoint, int triggerOccurrence = 1)
        {
            this.inner = new InMemoryOrchestrationStorageProvider(new SystemTextJsonSerializer());
            this.Leases = new LeaseFaultingLeaseStore(this.inner.Leases);
            this.Instances = new WaitBoundaryFaultingInstanceStore(this.inner.Instances, this.Leases, faultPoint, triggerOccurrence);
            this.History = new WaitBoundaryFaultingHistoryStore(this.inner.History, this.Leases, faultPoint, triggerOccurrence);
        }

        public IOrchestrationInstanceStore Instances { get; }

        public LeaseFaultingLeaseStore Leases { get; }

        IOrchestrationLeaseStore IOrchestrationStorageProvider.Leases => this.Leases;

        public IOrchestrationHistoryStore History { get; }

        public IOrchestrationSignalStore Signals => this.inner.Signals;

        public IOrchestrationTimerStore Timers => this.inner.Timers;

        public IOrchestrationQueryStore Queries => this.inner.Queries;

        public IOrchestrationAdministrationStore Administration => this.inner.Administration;

        public ISerializer Serializer => this.inner.Serializer;
    }

    private sealed class LeaseFaultingLeaseStore : IOrchestrationLeaseStore
    {
        private readonly IOrchestrationLeaseStore inner;
        private readonly ConcurrentDictionary<Guid, Guid> activeLeaseIds = new();
        private readonly ConcurrentDictionary<(Guid InstanceId, Guid LeaseId), byte> invalidatedLeases = new();

        public LeaseFaultingLeaseStore(IOrchestrationLeaseStore inner)
        {
            this.inner = inner;
        }

        public async Task<OrchestrationLease> AcquireAsync(Guid instanceId, string owner, TimeSpan duration, CancellationToken cancellationToken = default)
        {
            var lease = await this.inner.AcquireAsync(instanceId, owner, duration, cancellationToken).ConfigureAwait(false);
            this.activeLeaseIds[instanceId] = lease.LeaseId;
            return lease;
        }

        public void InvalidateActiveLease(Guid instanceId)
        {
            if (this.activeLeaseIds.TryGetValue(instanceId, out var leaseId))
            {
                this.invalidatedLeases.TryAdd((instanceId, leaseId), 0);
            }
        }

        public async Task<OrchestrationLease> RenewAsync(Guid instanceId, Guid leaseId, string owner, TimeSpan duration, CancellationToken cancellationToken = default)
        {
            var lease = await this.inner.RenewAsync(instanceId, leaseId, owner, duration, cancellationToken).ConfigureAwait(false);
            this.activeLeaseIds[instanceId] = lease.LeaseId;
            return lease;
        }

        public async Task ReleaseAsync(Guid instanceId, Guid leaseId, string owner, CancellationToken cancellationToken = default)
        {
            this.activeLeaseIds.TryRemove(instanceId, out _);
            this.invalidatedLeases.TryRemove((instanceId, leaseId), out _);
            await this.inner.ReleaseAsync(instanceId, leaseId, owner, cancellationToken).ConfigureAwait(false);
        }

        public async Task<bool> VerifyAsync(Guid instanceId, Guid leaseId, string owner, CancellationToken cancellationToken = default)
        {
            if (this.invalidatedLeases.ContainsKey((instanceId, leaseId)))
            {
                return false;
            }

            return await this.inner.VerifyAsync(instanceId, leaseId, owner, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class WaitBoundaryFaultingInstanceStore : IOrchestrationInstanceStore
    {
        private readonly IOrchestrationInstanceStore inner;
        private readonly LeaseFaultingLeaseStore leases;
        private readonly FaultPoint faultPoint;
        private readonly int triggerOccurrence;
        private int occurrence;

        public WaitBoundaryFaultingInstanceStore(
            IOrchestrationInstanceStore inner,
            LeaseFaultingLeaseStore leases,
            FaultPoint faultPoint,
            int triggerOccurrence)
        {
            this.inner = inner;
            this.leases = leases;
            this.faultPoint = faultPoint;
            this.triggerOccurrence = triggerOccurrence;
        }

        public Task<OrchestrationInstanceSnapshot> CreateAsync<TData>(OrchestrationContext<TData> context, string concurrencyKey = null, CancellationToken cancellationToken = default)
            where TData : class, IOrchestrationData
        {
            return this.inner.CreateAsync(context, concurrencyKey, cancellationToken);
        }

        public Task<OrchestrationInstanceSnapshot> GetAsync(Guid instanceId, CancellationToken cancellationToken = default)
        {
            return this.inner.GetAsync(instanceId, cancellationToken);
        }

        public async Task<OrchestrationInstanceSnapshot> SaveAsync<TData>(OrchestrationInstanceSnapshot snapshot, OrchestrationContext<TData> context, CancellationToken cancellationToken = default)
            where TData : class, IOrchestrationData
        {
            var persisted = await this.inner.SaveAsync(snapshot, context, cancellationToken).ConfigureAwait(false);
            if (this.faultPoint == FaultPoint.WaitingSnapshotSaved &&
                context.Status == OrchestrationStatus.Waiting &&
                context.Properties.Keys.Contains("__orchestration.wait.plan", StringComparer.OrdinalIgnoreCase) &&
                Interlocked.Increment(ref this.occurrence) == this.triggerOccurrence)
            {
                this.leases.InvalidateActiveLease(context.InstanceId);
            }

            return persisted;
        }
    }

    private sealed class WaitBoundaryFaultingHistoryStore : IOrchestrationHistoryStore
    {
        private readonly IOrchestrationHistoryStore inner;
        private readonly LeaseFaultingLeaseStore leases;
        private readonly FaultPoint faultPoint;
        private readonly int triggerOccurrence;
        private int occurrence;

        public WaitBoundaryFaultingHistoryStore(
            IOrchestrationHistoryStore inner,
            LeaseFaultingLeaseStore leases,
            FaultPoint faultPoint,
            int triggerOccurrence)
        {
            this.inner = inner;
            this.leases = leases;
            this.faultPoint = faultPoint;
            this.triggerOccurrence = triggerOccurrence;
        }

        public async Task<OrchestrationHistoryEntry> AppendAsync(OrchestrationHistoryEntry entry, CancellationToken cancellationToken = default)
        {
            var persisted = await this.inner.AppendAsync(entry, cancellationToken).ConfigureAwait(false);
            if (this.faultPoint == FaultPoint.TimerScheduledHistoryAppended &&
                string.Equals(entry.EventType, "TimerScheduled", StringComparison.OrdinalIgnoreCase) &&
                Interlocked.Increment(ref this.occurrence) == this.triggerOccurrence)
            {
                this.leases.InvalidateActiveLease(entry.InstanceId);
            }

            return persisted;
        }

        public Task<IReadOnlyCollection<OrchestrationHistoryEntry>> GetAsync(Guid instanceId, CancellationToken cancellationToken = default)
        {
            return this.inner.GetAsync(instanceId, cancellationToken);
        }
    }
}
