// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Orchestration;

using System.Reflection;
using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Orchestrations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

[IntegrationTest("Application")]
public class EntityFrameworkOrchestrationRuntimeTests(ITestOutputHelper output)
{
    [Fact]
    public async Task SweepOnceAsync_WhenStateTimeoutIsRecovered_CompletesWithEntityFrameworkPersistence()
    {
        var clock = new FakeOrchestrationClock();
        await using var fixture = await CreateFixtureAsync(clock);

        var instanceId = await CreateInstanceAsync<EfStateTimeoutOrchestration, EfRecoveryData>(fixture.ServiceProvider, new EfRecoveryData());
        await ContinueInstanceAsync(fixture.ServiceProvider, instanceId);

        clock.Advance(TimeSpan.FromMilliseconds(150));
        await fixture.Worker.SweepOnceAsync();

        var completed = await WaitForInstanceAsync(fixture.Queries, instanceId, item => item.Status == OrchestrationStatus.Completed);
        var context = await fixture.Queries.GetContextAsync<EfRecoveryData>(instanceId, fixture.ServiceProvider);

        completed.CurrentState.ShouldBe("Expired");
        context.Data.TimeoutExecutions.ShouldBe(1);
    }

    [Fact]
    public async Task SweepOnceAsync_WhenDelayWaitIsRecovered_DoesNotReplayActivityWithEntityFrameworkPersistence()
    {
        var clock = new FakeOrchestrationClock();
        await using var fixture = await CreateFixtureAsync(clock);

        var instanceId = await CreateInstanceAsync<EfDelayWaitOrchestration, EfRecoveryData>(fixture.ServiceProvider, new EfRecoveryData());
        await ContinueInstanceAsync(fixture.ServiceProvider, instanceId);

        clock.Advance(TimeSpan.FromMilliseconds(150));
        await fixture.Worker.SweepOnceAsync();

        var completed = await WaitForInstanceAsync(fixture.Queries, instanceId, item => item.Status == OrchestrationStatus.Completed);
        var context = await fixture.Queries.GetContextAsync<EfRecoveryData>(instanceId, fixture.ServiceProvider);

        completed.Status.ShouldBe(OrchestrationStatus.Completed);
        context.Data.DelayExecutions.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationCompensationFails_WithEntityFrameworkPersistence_ReportsFailedConsistently()
    {
        var clock = new FakeOrchestrationClock();
        await using var fixture = await CreateFixtureAsync(clock);

        var execute = await fixture.Runtime.ExecuteAsync<EfCancelCompensationFailureOrchestration, EfCompensationFailureData>(new EfCompensationFailureData());
        var wait = await fixture.Runtime.DispatchAndWaitAsync<EfCancelCompensationFailureOrchestration, EfCompensationFailureData>(
            new EfCompensationFailureData(),
            waitFor: WaitFor.Completion(),
            timeout: TimeSpan.FromSeconds(2));
        var snapshot = await fixture.Queries.GetInstanceAsync(wait.Value.InstanceId);
        var history = await fixture.Queries.GetHistoryAsync(wait.Value.InstanceId);

        execute.IsSuccess.ShouldBeTrue();
        execute.Value.Status.ShouldBe(nameof(OrchestrationStatus.Failed));
        execute.Value.Outcome.ShouldBe(nameof(OrchestrationStatus.Failed));
        wait.IsSuccess.ShouldBeTrue();
        wait.Value.Status.ShouldBe(nameof(OrchestrationStatus.Failed));
        wait.Value.Outcome.ShouldBe(nameof(OrchestrationStatus.Failed));
        snapshot.Status.ShouldBe(OrchestrationStatus.Failed);
        history.Last().EventType.ShouldBe("Failed");
    }

    [Fact]
    public async Task ExecuteAsync_WhenParallelStateReenters_RerunsBranchesWithEntityFrameworkPersistence()
    {
        var clock = new FakeOrchestrationClock();
        await using var fixture = await CreateFixtureAsync(clock);

        var result = await fixture.Runtime.ExecuteAsync<EfParallelReentryOrchestration, EfParallelReentryData>(new EfParallelReentryData());
        var context = await fixture.Queries.GetContextAsync<EfParallelReentryData>(result.Value.InstanceId, fixture.ServiceProvider);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(nameof(OrchestrationStatus.Completed));
        context.Data.VisitCount.ShouldBe(2);
        context.Data.LeftCount.ShouldBe(2);
        context.Data.RightCount.ShouldBe(2);
    }

    [Fact]
    public async Task SweepOnceAsync_WhenOneDueInstanceHasActiveLease_OtherEntityFrameworkInstancesStillAdvance()
    {
        var clock = new FakeOrchestrationClock();
        await using var fixture = await CreateFixtureAsync(clock);

        var leasedInstanceId = await CreateInstanceAsync<EfStateTimeoutOrchestration, EfRecoveryData>(fixture.ServiceProvider, new EfRecoveryData());
        var freeInstanceId = await CreateInstanceAsync<EfStateTimeoutOrchestration, EfRecoveryData>(fixture.ServiceProvider, new EfRecoveryData());

        await ContinueInstanceAsync(fixture.ServiceProvider, leasedInstanceId);
        await ContinueInstanceAsync(fixture.ServiceProvider, freeInstanceId);

        var leases = fixture.ServiceProvider.GetRequiredService<IOrchestrationLeaseStore>();
        var heldLease = await leases.AcquireAsync(leasedInstanceId, "held-worker", TimeSpan.FromMinutes(1));

        clock.Advance(TimeSpan.FromMilliseconds(150));
        await fixture.Worker.SweepOnceAsync();

        (await fixture.Queries.GetInstanceAsync(leasedInstanceId)).Status.ShouldBe(OrchestrationStatus.Waiting);
        (await WaitForInstanceAsync(fixture.Queries, freeInstanceId, item => item.Status == OrchestrationStatus.Completed)).CurrentState.ShouldBe("Expired");

        await leases.ReleaseAsync(leasedInstanceId, heldLease.LeaseId, heldLease.Owner);
        await fixture.Worker.SweepOnceAsync();

        (await WaitForInstanceAsync(fixture.Queries, leasedInstanceId, item => item.Status == OrchestrationStatus.Completed)).CurrentState.ShouldBe("Expired");
    }

    private async Task<TestFixture> CreateFixtureAsync(FakeOrchestrationClock clock)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        ConfigureLogging(services);
        services.AddSingleton<IHostApplicationLifetime, TestHostApplicationLifetime>();
        services.AddSingleton<IOrchestrationClock>(clock);
        services.AddSingleton(new OrchestrationExecutionSettings { EnableBackgroundExecution = false });
        services.AddDbContext<TestEntityFrameworkOrchestrationDbContext>(options => options.UseSqlite(connection));
        services.AddOrchestrations()
            .WithOrchestration<EfStateTimeoutOrchestration>()
            .WithOrchestration<EfDelayWaitOrchestration>()
            .WithOrchestration<EfCancelCompensationFailureOrchestration>()
            .WithOrchestration<EfParallelReentryOrchestration>()
            .WithEntityFramework<TestEntityFrameworkOrchestrationDbContext>();

        var serviceProvider = services.BuildServiceProvider();
        await serviceProvider.GetRequiredService<TestEntityFrameworkOrchestrationDbContext>().Database.EnsureCreatedAsync();

        return new TestFixture(
            serviceProvider,
            connection,
            serviceProvider.GetRequiredService<IOrchestrationService>(),
            serviceProvider.GetRequiredService<IOrchestrationQueryStore>(),
            serviceProvider.GetRequiredService<OrchestrationRecoveryService>());
    }

    private void ConfigureLogging(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
            builder.AddProvider(new XunitLoggerProvider(output));
        });
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

    private sealed class TestFixture(
        ServiceProvider serviceProvider,
        SqliteConnection connection,
        IOrchestrationService runtime,
        IOrchestrationQueryStore queries,
        OrchestrationRecoveryService worker) : IAsyncDisposable
    {
        public ServiceProvider ServiceProvider { get; } = serviceProvider;

        public SqliteConnection Connection { get; } = connection;

        public IOrchestrationService Runtime { get; } = runtime;

        public IOrchestrationQueryStore Queries { get; } = queries;

        public OrchestrationRecoveryService Worker { get; } = worker;

        public async ValueTask DisposeAsync()
        {
            await this.Connection.DisposeAsync();
            await this.ServiceProvider.DisposeAsync();
        }
    }

    private sealed class EfRecoveryData : IOrchestrationData
    {
        public int DelayExecutions { get; set; }

        public int TimeoutExecutions { get; set; }
    }

    private sealed class EfCompensationFailureData : IOrchestrationData
    {
    }

    private sealed class EfParallelReentryData : IOrchestrationData
    {
        public int VisitCount { get; set; }

        public int LeftCount { get; set; }

        public int RightCount { get; set; }
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
    }

    private sealed class EfStateTimeoutOrchestration : Orchestration<EfRecoveryData>
    {
        protected override void Define(IOrchestrationBuilder<EfRecoveryData> builder)
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

    private sealed class EfDelayWaitOrchestration : Orchestration<EfRecoveryData>
    {
        protected override void Define(IOrchestrationBuilder<EfRecoveryData> builder)
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

    private sealed class EfCancelCompensationFailureOrchestration : Orchestration<EfCompensationFailureData>
    {
        protected override void Define(IOrchestrationBuilder<EfCompensationFailureData> builder)
        {
            builder.State("Created", state => state
                .Activity(
                    (context, cancellationToken) => Task.FromResult(OrchestrationOutcome.Continue()),
                    activity => activity.CompensateWith(
                        (context, cancellationToken) => throw new InvalidOperationException("undo-failed"),
                        "UndoStep"),
                    "Step")
                .Cancel("cancel-requested"));
        }
    }

    private sealed class EfParallelReentryOrchestration : Orchestration<EfParallelReentryData>
    {
        protected override void Define(IOrchestrationBuilder<EfParallelReentryData> builder)
        {
            builder
                .State("Review", state => state
                    .Parallel(parallel => parallel
                        .Branch("Left", branch => branch.Activity(
                            (context, cancellationToken) =>
                            {
                                context.Data.LeftCount++;
                                return Task.FromResult(OrchestrationOutcome.Continue());
                            },
                            "LeftStep"))
                        .Branch("Right", branch => branch.Activity(
                            (context, cancellationToken) =>
                            {
                                context.Data.RightCount++;
                                return Task.FromResult(OrchestrationOutcome.Continue());
                            },
                            "RightStep"))
                        .JoinAll(),
                        "ParallelReview")
                    .TransformActivity(context => context.Data.VisitCount++, "CountVisit")
                    .TransitionTo("Review", context => context.Data.VisitCount < 2)
                    .TransitionTo("Done", context => context.Data.VisitCount >= 2))
                .State("Done", state => state.Complete());
        }
    }
}

public class TestEntityFrameworkOrchestrationDbContext(DbContextOptions<TestEntityFrameworkOrchestrationDbContext> options) : DbContext(options), IOrchestrationContext
{
    public DbSet<OrchestrationInstance> OrchestrationInstances { get; set; }

    public DbSet<OrchestrationHistory> OrchestrationHistory { get; set; }

    public DbSet<OrchestrationSignal> OrchestrationSignals { get; set; }

    public DbSet<OrchestrationTimer> OrchestrationTimers { get; set; }
}
