// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Orchestrations;

using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Orchestrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public abstract class EntityFrameworkOrchestrationStorageTestsBase
{
    protected abstract EntityFrameworkOrchestrationTestSupport Support { get; }

    [Fact]
    public virtual async Task CreateAndGetContextAsync_PersistsAndRestoresSnapshotConsistently()
    {
        var instances = this.Support.GetRequiredService<IOrchestrationInstanceStore>();
        var queries = this.Support.GetRequiredService<IOrchestrationQueryStore>();
        var instanceId = Guid.NewGuid();
        var startedUtc = DateTimeOffset.UtcNow.AddMinutes(-15);
        var context = this.CreateContext(
            instanceId,
            "OrderApproval",
            new TestOrchestrationData { OrderId = "ORD-42", Step = 2 },
            startedUtc,
            "corr-42",
            OrchestrationStatus.Waiting,
            "AwaitingApproval",
            "SendApprovalEmail");

        context.Properties["retryCount"] = 3;
        context.Properties["metadata"] = new TestOrchestrationProperty("northwind", 7);
        context.LastOutcome = OrchestrationOutcome.Wait(TimeSpan.FromMinutes(15), "awaiting approval");

        await instances.CreateAsync(context, "order-42");

        var snapshot = await queries.GetInstanceAsync(instanceId);
        var restored = await queries.GetContextAsync<TestOrchestrationData>(instanceId, this.Support.ServiceProvider);

        snapshot.ShouldNotBeNull();
        snapshot.InstanceId.ShouldBe(instanceId);
        snapshot.OrchestrationName.ShouldBe("OrderApproval");
        snapshot.Status.ShouldBe(OrchestrationStatus.Waiting);
        snapshot.CurrentState.ShouldBe("AwaitingApproval");
        snapshot.CurrentActivity.ShouldBe("SendApprovalEmail");
        snapshot.CorrelationId.ShouldBe("corr-42");
        snapshot.ConcurrencyKey.ShouldBe("order-42");
        snapshot.Version.ShouldBe(1);

        restored.ShouldNotBeNull();
        restored.InstanceId.ShouldBe(instanceId);
        restored.OrchestrationName.ShouldBe("OrderApproval");
        restored.Data.OrderId.ShouldBe("ORD-42");
        restored.Data.Step.ShouldBe(2);
        restored.Status.ShouldBe(OrchestrationStatus.Waiting);
        restored.CurrentState.ShouldBe("AwaitingApproval");
        restored.CurrentActivity.ShouldBe("SendApprovalEmail");
        restored.StartedUtc.ShouldBe(startedUtc);
        restored.LastOutcome.Kind.ShouldBe(OrchestrationOutcomeKind.Wait);
        restored.LastOutcome.Reason.ShouldBe("awaiting approval");
        restored.LastOutcome.Delay.ShouldBe(TimeSpan.FromMinutes(15));
        restored.Properties["retryCount"].ShouldBe(3);
        restored.Properties["metadata"].ShouldBeOfType<TestOrchestrationProperty>().Value.ShouldBe("northwind");
        restored.Properties["metadata"].ShouldBeOfType<TestOrchestrationProperty>().Attempt.ShouldBe(7);
    }

    [Fact]
    public virtual async Task ArchiveAndPurgeAsync_MaintainsAndRemovesRetainedOrchestrationData()
    {
        var instances = this.Support.GetRequiredService<IOrchestrationInstanceStore>();
        var history = this.Support.GetRequiredService<IOrchestrationHistoryStore>();
        var signals = this.Support.GetRequiredService<IOrchestrationSignalStore>();
        var timers = this.Support.GetRequiredService<IOrchestrationTimerStore>();
        var queries = this.Support.GetRequiredService<IOrchestrationQueryStore>();
        var admin = this.Support.GetRequiredService<IOrchestrationAdministrationStore>();
        var instanceId = Guid.NewGuid();

        await instances.CreateAsync(this.CreateContext(
            instanceId,
            "OrderApproval",
            new TestOrchestrationData { OrderId = "ORD-77", Step = 3 },
            DateTimeOffset.UtcNow.AddMinutes(-30),
            "corr-77",
            OrchestrationStatus.Completed,
            "Completed",
            "Finalize"), "order-77");

        await history.AppendAsync(new OrchestrationHistoryEntry { InstanceId = instanceId, EventType = "Completed" });
        await signals.PersistAsync(instanceId, "Approved", new TestSignalPayload("ok"));
        await timers.ScheduleAsync(instanceId, "WaitDelay", DateTimeOffset.UtcNow.AddMinutes(-5));

        (await admin.ArchiveAsync(instanceId)).ShouldBeTrue();

        var archived = await queries.GetInstanceAsync(instanceId);
        archived.ShouldNotBeNull();
        archived.IsArchived.ShouldBeTrue();
        archived.ArchivedUtc.ShouldNotBeNull();

        var purged = await admin.PurgeAsync(new OrchestrationPurgeCriteria
        {
            OlderThan = DateTimeOffset.UtcNow.AddMinutes(1),
            Statuses = new[] { OrchestrationStatus.Completed },
            IsArchived = true,
        });

        purged.PurgedInstanceCount.ShouldBe(1);
        (await queries.GetInstanceAsync(instanceId)).ShouldBeNull();
        (await queries.GetHistoryAsync(instanceId)).ShouldBeEmpty();
        (await queries.GetSignalsAsync(instanceId)).ShouldBeEmpty();
        (await queries.GetTimersAsync(instanceId)).ShouldBeEmpty();
    }

    [Fact]
    public virtual async Task ReleaseLeaseAndRequeueTimersAsync_PerformsRepairOperations()
    {
        var instances = this.Support.GetRequiredService<IOrchestrationInstanceStore>();
        var leases = this.Support.GetRequiredService<IOrchestrationLeaseStore>();
        var timers = this.Support.GetRequiredService<IOrchestrationTimerStore>();
        var queries = this.Support.GetRequiredService<IOrchestrationQueryStore>();
        var admin = this.Support.GetRequiredService<IOrchestrationAdministrationStore>();
        var instanceId = Guid.NewGuid();
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-10);

        await instances.CreateAsync(this.CreateContext(instanceId, "OrderApproval", new TestOrchestrationData { OrderId = "ORD-88", Step = 1 }, baseTime, "corr-88"), "order-88");
        var lease = await leases.AcquireAsync(instanceId, "worker-1", TimeSpan.FromMinutes(2));
        var timer = await timers.ScheduleAsync(instanceId, "WaitDelay", baseTime.AddMinutes(1), continuation: "resume-88");
        await timers.UpdateStatusAsync(timer.TimerId, OrchestrationTimerStatus.Consumed, "handled");

        await admin.ReleaseLeaseAsync(instanceId);
        (await leases.VerifyAsync(instanceId, lease.LeaseId, "worker-1")).ShouldBeFalse();

        var requeued = await admin.RequeueTimersAsync(instanceId);
        requeued.ShouldBe(1);

        var stored = await queries.GetTimersAsync(instanceId);
        stored.Single(item => item.TimerId == timer.TimerId).Status.ShouldBe(OrchestrationTimerStatus.Pending);
        stored.Single(item => item.TimerId == timer.TimerId).ProcessedUtc.ShouldBeNull();
    }

    [Fact]
    public virtual async Task SaveAsync_WithStaleSnapshot_ThrowsVersionMismatch()
    {
        var instances = this.Support.GetRequiredService<IOrchestrationInstanceStore>();
        var queries = this.Support.GetRequiredService<IOrchestrationQueryStore>();
        var instanceId = Guid.NewGuid();
        var context = this.CreateContext(instanceId, "OrderApproval");

        await instances.CreateAsync(context, "order-100");

        var snapshot1 = await instances.GetAsync(instanceId);
        var snapshot2 = await instances.GetAsync(instanceId);
        var updated = await queries.GetContextAsync<TestOrchestrationData>(instanceId, this.Support.ServiceProvider);
        updated.Status = OrchestrationStatus.Running;
        updated.CurrentState = "Reviewing";
        updated.CurrentActivity = "ReviewOrder";

        var persisted = await instances.SaveAsync(snapshot1, updated);

        persisted.Version.ShouldBe(2);
        await Should.ThrowAsync<InvalidOperationException>(() => instances.SaveAsync(snapshot2, updated));
    }

    [Fact]
    public virtual async Task AcquireRenewReleaseAsync_EnforcesExclusiveLeases()
    {
        var leases = this.Support.GetRequiredService<IOrchestrationLeaseStore>();
        var instanceId = await this.CreatePersistedInstanceAsync();

        var lease = await leases.AcquireAsync(instanceId, "worker-1", TimeSpan.FromMinutes(1));

        (await leases.VerifyAsync(instanceId, lease.LeaseId, "worker-1")).ShouldBeTrue();
        await Should.ThrowAsync<InvalidOperationException>(() => leases.AcquireAsync(instanceId, "worker-2", TimeSpan.FromMinutes(1)));

        var renewed = await leases.RenewAsync(instanceId, lease.LeaseId, "worker-1", TimeSpan.FromMinutes(2));
        renewed.ExpiresUtc.ShouldBeGreaterThan(lease.ExpiresUtc);

        await leases.ReleaseAsync(instanceId, renewed.LeaseId, "worker-1");
        (await leases.VerifyAsync(instanceId, renewed.LeaseId, "worker-1")).ShouldBeFalse();
    }

    [Fact]
    public virtual async Task AcquireAsync_WithCompetingWorkers_OnlyOneLeaseIsGranted()
    {
        var leases = this.Support.GetRequiredService<IOrchestrationLeaseStore>();
        var instanceId = await this.CreatePersistedInstanceAsync();

        async Task<OrchestrationLease> TryAcquireAsync(string owner)
        {
            try
            {
                return await leases.AcquireAsync(instanceId, owner, TimeSpan.FromMinutes(1));
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        var results = await Task.WhenAll(TryAcquireAsync("worker-a"), TryAcquireAsync("worker-b"));
        results.Count(item => item is not null).ShouldBe(1);

        var winner = results.Single(item => item is not null);
        await leases.ReleaseAsync(instanceId, winner.LeaseId, winner.Owner);
    }

    [Fact]
    public virtual async Task PersistSignalsAsync_SupportsIdempotencyAndStateFiltering()
    {
        var signals = this.Support.GetRequiredService<IOrchestrationSignalStore>();
        var queries = this.Support.GetRequiredService<IOrchestrationQueryStore>();
        var instanceId = await this.CreatePersistedInstanceAsync();

        var first = await signals.PersistAsync(instanceId, "Approved", new TestSignalPayload("yes"), "AwaitingApproval", "sig-1");
        var duplicate = await signals.PersistAsync(instanceId, "Approved", new TestSignalPayload("yes"), "AwaitingApproval", "sig-1");
        var otherState = await signals.PersistAsync(instanceId, "Approved", new TestSignalPayload("later"), "Escalated", "sig-2");
        var anyState = await signals.PersistAsync(instanceId, "Reminder", new TestSignalPayload("notify"));

        duplicate.SignalId.ShouldBe(first.SignalId);

        var processable = await signals.GetProcessableAsync(instanceId, "AwaitingApproval");

        processable.Select(item => item.SignalId).ShouldContain(first.SignalId);
        processable.Select(item => item.SignalId).ShouldContain(anyState.SignalId);
        processable.Select(item => item.SignalId).ShouldNotContain(otherState.SignalId);

        var processed = await signals.UpdateStatusAsync(first.SignalId, OrchestrationSignalStatus.Processed, "accepted");
        processed.Status.ShouldBe(OrchestrationSignalStatus.Processed);
        processed.StatusReason.ShouldBe("accepted");
        processed.ProcessedUtc.ShouldNotBeNull();

        var stored = await queries.GetSignalsAsync(instanceId);
        stored.Count.ShouldBe(3);
    }

    [Fact]
    public virtual async Task ScheduleTimersAsync_ReturnsDueTimersInDeterministicOrder()
    {
        var timers = this.Support.GetRequiredService<IOrchestrationTimerStore>();
        var queries = this.Support.GetRequiredService<IOrchestrationQueryStore>();
        var instanceId = await this.CreatePersistedInstanceAsync();
        var now = DateTimeOffset.UtcNow;

        var first = await timers.ScheduleAsync(instanceId, "WaitDelay", now.AddMinutes(-5), continuation: "s1");
        var second = await timers.ScheduleAsync(instanceId, "WaitDelay", now.AddMinutes(-1), continuation: "s2");
        var future = await timers.ScheduleAsync(instanceId, "StateTimeout", now.AddMinutes(10), targetState: "Escalated", continuation: "s3");

        var due = await timers.GetDueAsync(now);

        due.Select(item => item.TimerId).ToArray().ShouldBe([first.TimerId, second.TimerId]);

        var consumed = await timers.UpdateStatusAsync(first.TimerId, OrchestrationTimerStatus.Consumed, "handled");
        consumed.Status.ShouldBe(OrchestrationTimerStatus.Consumed);

        var stored = await queries.GetTimersAsync(instanceId);
        stored.Count.ShouldBe(3);
        stored.Single(item => item.TimerId == future.TimerId).Status.ShouldBe(OrchestrationTimerStatus.Pending);
    }

    [Fact]
    public virtual async Task QueryAsync_FiltersAndPagesPersistedInstances()
    {
        var queries = this.Support.GetRequiredService<IOrchestrationQueryStore>();
        await this.CreatePersistedInstanceAsync("OrderApproval", OrchestrationStatus.Waiting, "AwaitingApproval", "corr-1", "order-1", DateTimeOffset.UtcNow.AddMinutes(-30));
        await this.CreatePersistedInstanceAsync("OrderApproval", OrchestrationStatus.Waiting, "AwaitingApproval", "corr-2", "order-2", DateTimeOffset.UtcNow.AddMinutes(-10));
        await this.CreatePersistedInstanceAsync("TelephoneCall", OrchestrationStatus.Completed, "Destroyed", "corr-3", "call-3", DateTimeOffset.UtcNow.AddMinutes(-20));

        var result = await queries.QueryAsync(new OrchestrationInstanceQuery
        {
            OrchestrationName = "OrderApproval",
            Statuses = [OrchestrationStatus.Waiting],
            States = ["AwaitingApproval"],
            Skip = 0,
            Take = 1,
        });

        result.TotalCount.ShouldBe(2);
        result.Items.Count.ShouldBe(1);
        result.Items.Single().CorrelationId.ShouldBe("corr-2");
    }

    [Fact]
    public virtual async Task GetMetricsAsync_ReturnsCountsDerivedFromPersistedState()
    {
        var history = this.Support.GetRequiredService<IOrchestrationHistoryStore>();
        var signals = this.Support.GetRequiredService<IOrchestrationSignalStore>();
        var timers = this.Support.GetRequiredService<IOrchestrationTimerStore>();
        var queries = this.Support.GetRequiredService<IOrchestrationQueryStore>();

        var runningId = await this.CreatePersistedInstanceAsync("OrderApproval", OrchestrationStatus.Running, "Reviewing", "corr-running", "order-running", DateTimeOffset.UtcNow.AddMinutes(-20));
        var waitingId = await this.CreatePersistedInstanceAsync("OrderApproval", OrchestrationStatus.Waiting, "AwaitingApproval", "corr-waiting", "order-waiting", DateTimeOffset.UtcNow.AddMinutes(-40));
        var completedId = await this.CreatePersistedInstanceAsync("TelephoneCall", OrchestrationStatus.Completed, "Destroyed", "corr-completed", "call-completed", DateTimeOffset.UtcNow.AddMinutes(-5));
        var terminatedId = await this.CreatePersistedInstanceAsync("TelephoneCall", OrchestrationStatus.Terminated, "Rejected", "corr-terminated", "call-terminated", DateTimeOffset.UtcNow.AddMinutes(-3));

        await history.AppendAsync(new OrchestrationHistoryEntry { InstanceId = runningId, EventType = "Created", StateName = "Reviewing" });
        await history.AppendAsync(new OrchestrationHistoryEntry { InstanceId = waitingId, EventType = "Waiting", StateName = "AwaitingApproval" });
        await signals.PersistAsync(waitingId, "Approved", new TestSignalPayload("yes"), "AwaitingApproval", "sig-a");
        await signals.PersistAsync(completedId, "Archived", new TestSignalPayload("done"), idempotencyKey: "sig-b");
        await timers.ScheduleAsync(waitingId, "WaitDelay", DateTimeOffset.UtcNow.AddMinutes(1), continuation: "t1");

        var metrics = await queries.GetMetricsAsync();

        metrics.TotalInstances.ShouldBe(4);
        metrics.RunningInstances.ShouldBe(1);
        metrics.WaitingInstances.ShouldBe(1);
        metrics.CompletedInstances.ShouldBe(1);
        metrics.TerminatedInstances.ShouldBe(1);
        metrics.HistoryCount.ShouldBe(2);
        metrics.SignalCount.ShouldBe(2);
        metrics.TimerCount.ShouldBe(1);
        metrics.OldestWaitingStartedUtc.ShouldNotBeNull();
        metrics.OldestWaitingStartedUtc.Value.ShouldBeLessThan(DateTimeOffset.UtcNow.AddMinutes(-30));
        metrics.InstanceCountsByOrchestration["OrderApproval"].ShouldBe(2);
        metrics.InstanceCountsByOrchestration["TelephoneCall"].ShouldBe(2);
    }

    [Fact]
    public virtual async Task AppendHistoryAsync_WhenBackdatedEntryIsAppended_PreservesMonotonicAppendOrder()
    {
        var history = this.Support.GetRequiredService<IOrchestrationHistoryStore>();
        var queries = this.Support.GetRequiredService<IOrchestrationQueryStore>();
        var instanceId = await this.CreatePersistedInstanceAsync();
        var older = DateTimeOffset.UtcNow.AddMinutes(-2);
        var newer = DateTimeOffset.UtcNow.AddMinutes(-1);

        await history.AppendAsync(new OrchestrationHistoryEntry
        {
            InstanceId = instanceId,
            EventType = "Created",
            StateName = "Submitted",
            RecordedAt = newer,
        });

        await history.AppendAsync(new OrchestrationHistoryEntry
        {
            InstanceId = instanceId,
            EventType = "Waiting",
            StateName = "AwaitingApproval",
            RecordedAt = older,
        });

        var stored = (await queries.GetHistoryAsync(instanceId)).ToArray();
        stored.Select(item => item.EventType).ToArray().ShouldBe(["Created", "Waiting"]);
        stored[0].RecordedAt.ShouldBeLessThan(stored[1].RecordedAt);
    }

    protected OrchestrationContext<TestOrchestrationData> CreateContext(
        Guid instanceId,
        string orchestrationName,
        TestOrchestrationData data = null,
        DateTimeOffset? startedUtc = null,
        string correlationId = null,
        OrchestrationStatus status = OrchestrationStatus.Created,
        string currentState = "Submitted",
        string currentActivity = "Create")
    {
        var context = new OrchestrationContext<TestOrchestrationData>(
            orchestrationName,
            data ?? new TestOrchestrationData { OrderId = $"ORD-{instanceId:N}", Step = 1 },
            this.Support.ServiceProvider,
            instanceId,
            correlationId,
            startedUtc)
        {
            Status = status,
            CurrentState = currentState,
            CurrentActivity = currentActivity,
        };

        if (status is OrchestrationStatus.Completed or OrchestrationStatus.Cancelled or OrchestrationStatus.Failed or OrchestrationStatus.Terminated)
        {
            context.CompletedUtc = (startedUtc ?? DateTimeOffset.UtcNow).AddMinutes(1);
        }

        return context;
    }

    protected async Task<Guid> CreatePersistedInstanceAsync(
        string orchestrationName = "OrderApproval",
        OrchestrationStatus status = OrchestrationStatus.Waiting,
        string currentState = "AwaitingApproval",
        string correlationId = null,
        string concurrencyKey = null,
        DateTimeOffset? startedUtc = null)
    {
        var instances = this.Support.GetRequiredService<IOrchestrationInstanceStore>();
        var instanceId = Guid.NewGuid();
        var context = this.CreateContext(
            instanceId,
            orchestrationName,
            startedUtc: startedUtc,
            correlationId: correlationId ?? instanceId.ToString("N"),
            status: status,
            currentState: currentState,
            currentActivity: "Advance");

        await instances.CreateAsync(context, concurrencyKey);

        return instanceId;
    }
}

public sealed class EntityFrameworkOrchestrationTestSupport : IDisposable
{
    public EntityFrameworkOrchestrationTestSupport(
        ITestOutputHelper output,
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(configureDbContext);

        this.LoggerFactory = XunitLoggerFactory.Create(output);

        var services = new ServiceCollection();
        services.AddSingleton(this.LoggerFactory);
        services.AddLogging();
        services.AddDbContext<OrchestrationTestDbContext>(configureDbContext);
        services.AddOrchestrations().WithEntityFramework<OrchestrationTestDbContext>();

        this.ServiceProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        using var scope = this.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrchestrationTestDbContext>();
        EnsureOrchestrationTablesCreated(dbContext);
        dbContext.OrchestrationHistory.RemoveRange(dbContext.OrchestrationHistory);
        dbContext.OrchestrationSignals.RemoveRange(dbContext.OrchestrationSignals);
        dbContext.OrchestrationTimers.RemoveRange(dbContext.OrchestrationTimers);
        dbContext.OrchestrationInstances.RemoveRange(dbContext.OrchestrationInstances);
        dbContext.SaveChanges();
    }

    public ILoggerFactory LoggerFactory { get; }

    public ServiceProvider ServiceProvider { get; }

    public T GetRequiredService<T>()
        where T : notnull
    {
        return this.ServiceProvider.GetRequiredService<T>();
    }

    public async Task<TResult> ExecuteDbContextAsync<TResult>(Func<OrchestrationTestDbContext, Task<TResult>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        using var scope = this.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrchestrationTestDbContext>();
        return await action(dbContext);
    }

    public void Dispose()
    {
        this.ServiceProvider.Dispose();
        this.LoggerFactory.Dispose();
    }

    private static void EnsureOrchestrationTablesCreated(OrchestrationTestDbContext dbContext)
    {
        dbContext.Database.EnsureCreated();

        if (TableExists(dbContext, dbContext.Model.FindEntityType(typeof(OrchestrationInstance))?.GetTableName()))
        {
            return;
        }

        dbContext.GetService<IRelationalDatabaseCreator>().CreateTables();
    }

    private static bool TableExists(DbContext dbContext, string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return true;
        }

        var providerName = dbContext.Database.ProviderName;
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;

        try
        {
            if (shouldClose)
            {
                connection.Open();
            }

            using var command = connection.CreateCommand();
            command.CommandText = providerName switch
            {
                string name when name.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) =>
                    "SELECT 1 FROM sys.tables WHERE name = @name",
                string name when name.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) =>
                    "SELECT 1 FROM information_schema.tables WHERE table_schema = current_schema() AND table_name = @name",
                string name when name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) =>
                    "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = @name",
                _ => throw new InvalidOperationException($"Unsupported provider '{providerName}' for orchestration table checks.")
            };

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@name";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);

            return command.ExecuteScalar() is not null and not DBNull;
        }
        finally
        {
            if (shouldClose)
            {
                connection.Close();
            }
        }
    }
}

public class OrchestrationTestDbContext(DbContextOptions<OrchestrationTestDbContext> options) : DbContext(options), IOrchestrationContext
{
    public DbSet<OrchestrationInstance> OrchestrationInstances { get; set; }

    public DbSet<OrchestrationHistory> OrchestrationHistory { get; set; }

    public DbSet<OrchestrationSignal> OrchestrationSignals { get; set; }

    public DbSet<OrchestrationTimer> OrchestrationTimers { get; set; }
}

public sealed class TestOrchestrationData : IOrchestrationData
{
    public string OrderId { get; set; }

    public int Step { get; set; }
}

public sealed record TestOrchestrationProperty(string Value, int Attempt);

public sealed record TestSignalPayload(string Decision);
