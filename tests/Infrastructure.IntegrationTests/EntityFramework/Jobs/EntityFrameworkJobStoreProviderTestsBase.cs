// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Jobs;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

[IntegrationTest("Infrastructure")]
public abstract class EntityFrameworkJobStoreProviderTestsBase
{
    protected abstract EntityFrameworkJobSchedulerTestSupport Support { get; }

    [Fact]
    public async Task DbContextCanHostSchedulerSetsAndExposeRequiredIndexes()
    {
        await using var scope = this.Support.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TestJobSchedulerDbContext>();

        context.AppEntities.Add(new TestAppEntity { Name = "app" });
        await context.SaveChangesAsync();

        context.Model.FindEntityType(typeof(TestAppEntity)).ShouldNotBeNull();
        context.Model.FindEntityType(typeof(JobRuntimeStateEntity)).ShouldNotBeNull();
        context.Model.FindEntityType(typeof(JobTriggerRuntimeStateEntity)).ShouldNotBeNull();
        context.Model.FindEntityType(typeof(JobOccurrenceEntity)).ShouldNotBeNull();
        context.Model.FindEntityType(typeof(JobOccurrenceDependencyEntity)).ShouldNotBeNull();
        context.Model.FindEntityType(typeof(JobBatchEntity)).ShouldNotBeNull();
        context.Model.FindEntityType(typeof(JobBatchOccurrenceEntity)).ShouldNotBeNull();
        context.Model.FindEntityType(typeof(JobExecutionEntity)).ShouldNotBeNull();
        context.Model.FindEntityType(typeof(JobExecutionHistoryEntity)).ShouldNotBeNull();
        context.Model.FindEntityType(typeof(JobBatchHistoryEntity)).ShouldNotBeNull();
        context.Model.FindEntityType(typeof(JobLeaseEntity)).ShouldNotBeNull();

        var occurrenceEntity = context.Model.FindEntityType(typeof(JobOccurrenceEntity));
        var batchEntity = context.Model.FindEntityType(typeof(JobBatchEntity));
        var historyEntity = context.Model.FindEntityType(typeof(JobExecutionHistoryEntity));
        var batchHistoryEntity = context.Model.FindEntityType(typeof(JobBatchHistoryEntity));
        var leaseEntity = context.Model.FindEntityType(typeof(JobLeaseEntity));

        AssertHasIndex(occurrenceEntity!, true, nameof(JobOccurrenceEntity.OccurrenceKey));
        AssertHasIndex(occurrenceEntity!, false, nameof(JobOccurrenceEntity.Status), nameof(JobOccurrenceEntity.DueUtc));
        AssertHasIndex(batchEntity!, true, nameof(JobBatchEntity.ExternalBatchId));
        AssertHasIndex(batchEntity!, false, nameof(JobBatchEntity.ArchivedDate));
        AssertHasIndex(historyEntity!, false, nameof(JobExecutionHistoryEntity.OccurrenceId), nameof(JobExecutionHistoryEntity.RecordedAt));
        AssertHasIndex(batchHistoryEntity!, false, nameof(JobBatchHistoryEntity.BatchId), nameof(JobBatchHistoryEntity.RecordedAt));
        AssertHasIndex(leaseEntity!, false, nameof(JobLeaseEntity.ExpiresUtc));
    }

    [Fact]
    public async Task OccurrenceDeduplicationAndLeaseOwnershipChecks_AreEnforced()
    {
        var store = this.Support.Store;
        var dueUtc = this.Support.Clock.GetUtcNow();
        var initialOccurrence = CreateOccurrence("occ-001", dueUtc);

        var created = await store.Occurrences.TryCreateAsync(initialOccurrence);
        var duplicate = await store.Occurrences.TryCreateAsync(CreateOccurrence("occ-001", dueUtc, occurrenceId: Guid.NewGuid()));

        var persisted = await store.Occurrences.GetByKeyAsync("occ-001");
        var firstLease = await store.Leases.TryAcquireAsync(persisted.OccurrenceId, "node-a", TimeSpan.FromMinutes(5));
        var competingLease = await store.Leases.TryAcquireAsync(persisted.OccurrenceId, "node-b", TimeSpan.FromMinutes(5));
        var renewedLease = await store.Leases.RenewAsync(firstLease!.OccurrenceId, "node-a", firstLease.OwnershipToken, TimeSpan.FromMinutes(10));
        var owned = await store.Leases.VerifyOwnershipAsync(firstLease.OccurrenceId, "node-a", renewedLease!.OwnershipToken);
        var released = await store.Leases.ReleaseAsync(firstLease.OccurrenceId, "node-a", renewedLease.OwnershipToken);
        var ownedAfterRelease = await store.Leases.VerifyOwnershipAsync(firstLease.OccurrenceId, "node-a", renewedLease.OwnershipToken);

        created.ShouldBeTrue();
        duplicate.ShouldBeFalse();
        firstLease.ShouldNotBeNull();
        competingLease.ShouldBeNull();
        renewedLease.ShouldNotBeNull();
        renewedLease.ExpiresUtc.ShouldBeGreaterThan(firstLease.ExpiresUtc);
        owned.ShouldBeTrue();
        released.ShouldBeTrue();
        ownedAfterRelease.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecutionHistoryAndPreviousSuccessfulExecution_ArePersisted()
    {
        var store = this.Support.Store;
        var earlierUtc = this.Support.Clock.GetUtcNow().AddMinutes(-10);
        var laterUtc = this.Support.Clock.GetUtcNow();
        var previousOccurrence = CreateOccurrence("prev-occ", earlierUtc, jobName: "success-job", triggerName: "manual", occurrenceId: Guid.NewGuid());
        var currentOccurrence = CreateOccurrence("curr-occ", laterUtc, jobName: "success-job", triggerName: "manual", occurrenceId: Guid.NewGuid());
        var previousExecutionId = Guid.NewGuid();

        await store.Occurrences.TryCreateAsync(previousOccurrence);
        await store.Occurrences.TryCreateAsync(currentOccurrence);
        await store.Executions.CreateAsync(new JobExecution
        {
            ExecutionId = previousExecutionId,
            OccurrenceId = previousOccurrence.OccurrenceId,
            JobName = previousOccurrence.JobName,
            TriggerName = previousOccurrence.TriggerName,
            AttemptNumber = 1,
            Status = JobExecutionStatus.Completed,
            SchedulerInstanceId = "node-a",
            StartedUtc = earlierUtc,
            CompletedUtc = earlierUtc.AddMinutes(1),
            Message = "completed",
            CreatedDate = earlierUtc,
            UpdatedDate = earlierUtc.AddMinutes(1),
        });
        await store.Executions.CreateAsync(new JobExecution
        {
            ExecutionId = Guid.NewGuid(),
            OccurrenceId = currentOccurrence.OccurrenceId,
            JobName = currentOccurrence.JobName,
            TriggerName = currentOccurrence.TriggerName,
            AttemptNumber = 1,
            Status = JobExecutionStatus.Started,
            SchedulerInstanceId = "node-a",
            StartedUtc = laterUtc,
            CreatedDate = laterUtc,
            UpdatedDate = laterUtc,
        });
        await store.ExecutionHistory.AppendAsync(new JobExecutionHistoryEntry
        {
            HistoryId = Guid.NewGuid(),
            OccurrenceId = previousOccurrence.OccurrenceId,
            ExecutionId = previousExecutionId,
            JobName = previousOccurrence.JobName,
            TriggerName = previousOccurrence.TriggerName,
            SchedulerInstanceId = "node-a",
            EventName = "ExecutionCompleted",
            OccurrenceStatus = JobOccurrenceStatus.Completed,
            ExecutionStatus = JobExecutionStatus.Completed,
            Message = "completed",
            RecordedAt = earlierUtc.AddMinutes(1),
            RecordedBy = "tester",
            Properties = new PropertyBag { ["source"] = "integration" },
        });

        var history = await store.ExecutionHistory.ListAsync(previousOccurrence.OccurrenceId);
        var previousExecution = await store.PreviousExecutions.GetPreviousSuccessfulExecutionAsync("success-job", "manual", laterUtc.AddMinutes(1));

        history.Count.ShouldBe(1);
        history[0].EventName.ShouldBe("ExecutionCompleted");
        history[0].Properties["source"].ShouldBe("integration");
        previousExecution.ShouldNotBeNull();
        previousExecution.ExecutionId.ShouldBe(previousExecutionId);
    }

    [Fact]
    public async Task DependencyPersistenceAndBatchAttachRemainAtomic()
    {
        var store = this.Support.Store;
        var dueUtc = this.Support.Clock.GetUtcNow();
        var batch = CreateBatch("batch-atomic", dueUtc);
        var first = CreateOccurrence("batch-occ-1", dueUtc, occurrenceId: Guid.NewGuid());
        var second = CreateOccurrence("batch-occ-2", dueUtc, occurrenceId: Guid.NewGuid());
        var memberships = new[]
        {
            CreateMembership(batch.BatchId, first.OccurrenceId, 0, first.Status, dueUtc),
            CreateMembership(batch.BatchId, second.OccurrenceId, 1, second.Status, dueUtc),
        };

        var created = await store.Batches.TryCreateWithChildrenAsync(batch, [first, second], memberships);

        var dependency = new JobOccurrenceDependency
        {
            DependencyId = Guid.NewGuid(),
            DependentOccurrenceId = second.OccurrenceId,
            PrerequisiteOccurrenceId = first.OccurrenceId,
            RequiredStatuses = [JobOccurrenceStatus.Completed],
            Status = JobDependencyStatus.Pending,
            FailurePolicy = JobDependencyFailurePolicy.Fail,
            Reason = "waiting",
            Properties = new PropertyBag { ["kind"] = "chain" },
            CreatedDate = dueUtc,
            UpdatedDate = dueUtc,
        };
        await store.Dependencies.AddAsync(dependency);
        await store.Dependencies.UpdateAsync(dependency with { Status = JobDependencyStatus.Failed, Reason = "failed prerequisite", UpdatedDate = dueUtc.AddMinutes(1) });

        var conflicting = CreateOccurrence("batch-occ-1", dueUtc, occurrenceId: Guid.NewGuid());
        var newChild = CreateOccurrence("batch-occ-3", dueUtc, occurrenceId: Guid.NewGuid());
        var attached = await store.Batches.TryAttachChildrenAsync(
            batch.BatchId,
            [newChild, conflicting],
            [CreateMembership(batch.BatchId, newChild.OccurrenceId, 2, newChild.Status, dueUtc)]);

        created.ShouldBeTrue();
        (await store.Dependencies.ListByDependentAsync(second.OccurrenceId)).Single().Status.ShouldBe(JobDependencyStatus.Failed);
        (await store.Dependencies.ListByPrerequisiteAsync(first.OccurrenceId)).Single().Reason.ShouldBe("failed prerequisite");
        attached.ShouldBeFalse();
        (await store.Occurrences.GetByKeyAsync("batch-occ-3")).ShouldBeNull();
        (await store.Batches.ListOccurrencesAsync(batch.BatchId)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task RuntimeCanRepairStaleBatchRollupWithEntityFrameworkPersistence()
    {
        var scheduler = this.Support.GetRequiredService<IJobSchedulerService>();
        var background = this.Support.GetRequiredService<JobSchedulerBackgroundService>();

        var dispatch = await scheduler.DispatchBatchAsync(new JobBatchDispatchRequest
        {
            BatchId = "batch-rollup-repair",
            CompletionPolicy = JobBatchCompletionPolicy.AllowPartialCompletion,
            Items =
            [
                new JobBatchDispatchItem { JobName = "success-job", Data = Unit.Value },
                new JobBatchDispatchItem { JobName = "fail-job", Data = Unit.Value },
            ],
        });

        dispatch.IsSuccess.ShouldBeTrue();
        for (var index = 0; index < 4; index++)
        {
            await background.SweepOnceAsync();
        }

        var persistedBatch = (await this.Support.Store.Batches.ListAsync()).Single(x => x.ExternalBatchId == dispatch.Value.BatchId);
        persistedBatch.Status.ShouldBe(JobBatchStatus.CompletedWithFailures);

        await this.Support.ExecuteDbContextAsync(async dbContext =>
        {
            var staleBatch = await dbContext.JobBatches.SingleAsync(x => x.BatchId == persistedBatch.BatchId);
            staleBatch.Status = JobBatchStatus.Processing;
            staleBatch.AcceptedCount = 0;
            staleBatch.SucceededCount = 0;
            staleBatch.FailedCount = 0;
            staleBatch.CancelledCount = 0;
            staleBatch.ArchivedCount = 0;
            staleBatch.CompletedDate = null;
            staleBatch.UpdatedDate = this.Support.Clock.GetUtcNow().AddMinutes(5);
            await dbContext.SaveChangesAsync();
        });

        var pause = await scheduler.PauseBatchAsync("batch-rollup-repair", "repair");
        pause.IsSuccess.ShouldBeTrue();

        var repairedBatch = await this.Support.Store.Batches.GetAsync(persistedBatch.BatchId);
        repairedBatch.Status.ShouldBe(JobBatchStatus.CompletedWithFailures);
        repairedBatch.AcceptedCount.ShouldBe(2);
        repairedBatch.SucceededCount.ShouldBe(1);
        repairedBatch.FailedCount.ShouldBe(1);
    }

    protected static JobOccurrence CreateOccurrence(
        string occurrenceKey,
        DateTimeOffset dueUtc,
        string jobName = "success-job",
        string triggerName = "manual",
        Guid? occurrenceId = null)
        => new()
        {
            OccurrenceId = occurrenceId ?? Guid.NewGuid(),
            OccurrenceKey = occurrenceKey,
            JobName = jobName,
            TriggerName = triggerName,
            TriggerType = JobTriggerType.Manual,
            Status = JobOccurrenceStatus.Due,
            DueUtc = dueUtc,
            ScheduledUtc = dueUtc,
            Data = Unit.Value,
            DataType = typeof(Unit),
            Properties = new PropertyBag { ["source"] = "integration" },
            CorrelationId = $"corr-{occurrenceKey}",
            CausationId = $"cause-{occurrenceKey}",
            IdempotencyKey = occurrenceKey,
            CreatedDate = dueUtc,
            UpdatedDate = dueUtc,
        };

    protected static JobBatch CreateBatch(string externalBatchId, DateTimeOffset nowUtc)
        => new()
        {
            BatchId = Guid.NewGuid(),
            ExternalBatchId = externalBatchId,
            Description = "batch",
            Status = JobBatchStatus.Processing,
            CompletionPolicy = JobBatchCompletionPolicy.AllowPartialCompletion,
            Properties = new PropertyBag { ["scope"] = "integration" },
            CorrelationId = $"corr-{externalBatchId}",
            CausationId = $"cause-{externalBatchId}",
            IdempotencyKey = externalBatchId,
            AcceptedCount = 2,
            CreatedDate = nowUtc,
            UpdatedDate = nowUtc,
        };

    protected static JobBatchOccurrence CreateMembership(Guid batchId, Guid occurrenceId, int sequence, JobOccurrenceStatus status, DateTimeOffset nowUtc)
        => new()
        {
            BatchId = batchId,
            OccurrenceId = occurrenceId,
            ChildStatus = status,
            Sequence = sequence,
            CreatedDate = nowUtc,
            UpdatedDate = nowUtc,
        };

    protected static void AssertHasIndex(IEntityType entityType, bool unique, params string[] properties)
    {
        var index = entityType.GetIndexes().FirstOrDefault(x => x.Properties.Select(p => p.Name).SequenceEqual(properties));
        index.ShouldNotBeNull();
        index.IsUnique.ShouldBe(unique);
    }
}

public sealed class EntityFrameworkJobSchedulerTestSupport : IDisposable
{
    private readonly Action disposeAction;

    public EntityFrameworkJobSchedulerTestSupport(
        ITestOutputHelper output,
        Action<DbContextOptionsBuilder> configureDbContext,
        Action<IServiceCollection> configureServices = null,
        Action<JobBuilderContext> configureJobs = null,
        Action disposeAction = null)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(configureDbContext);

        this.disposeAction = disposeAction;
        this.Clock = new TestTimeProvider(new DateTimeOffset(2026, 05, 26, 09, 00, 00, TimeSpan.Zero));

        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
            builder.AddProvider(new XunitLoggerProvider(output));
        });
        services.AddSingleton<IHostApplicationLifetime, TestHostApplicationLifetime>();
        services.AddSingleton<TimeProvider>(this.Clock);
        services.AddDbContext<TestJobSchedulerDbContext>(configureDbContext);
        configureServices?.Invoke(services);

        var jobs = services.AddJobScheduler()
            .WithBackgroundExecution(options => options.EnableBackgroundExecution = false)
            .WithJob<SuccessJob>("success-job", job => job
                .Description("success")
                .AddTrigger("manual", trigger => trigger.Manual()))
            .WithJob<FailJob>("fail-job", job => job
                .Description("fail")
                .AddTrigger("manual", trigger => trigger.Manual()));

        configureJobs?.Invoke(jobs);
        jobs.WithEntityFramework<TestJobSchedulerDbContext>();

        this.ServiceProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        this.LoggerFactory = this.ServiceProvider.GetRequiredService<ILoggerFactory>();
        this.Store = this.ServiceProvider.GetRequiredService<IJobStoreProvider>();

        using var scope = this.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TestJobSchedulerDbContext>();
        EnsureJobSchedulerTablesCreated(dbContext);
        ClearTables(dbContext);
    }

    public ILoggerFactory LoggerFactory { get; private set; }

    public ServiceProvider ServiceProvider { get; }

    public TestTimeProvider Clock { get; }

    public IJobStoreProvider Store { get; }

    public AsyncServiceScope CreateAsyncScope() => this.ServiceProvider.CreateAsyncScope();

    public T GetRequiredService<T>()
        where T : notnull
        => this.ServiceProvider.GetRequiredService<T>();

    public async Task ExecuteDbContextAsync(Func<TestJobSchedulerDbContext, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        await using var scope = this.ServiceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TestJobSchedulerDbContext>();
        await action(dbContext);
    }

    public void Dispose()
    {
        this.ServiceProvider.Dispose();
        this.disposeAction?.Invoke();
    }

    private static void EnsureJobSchedulerTablesCreated(TestJobSchedulerDbContext dbContext)
    {
        dbContext.Database.EnsureCreated();

        if (TableExists(dbContext, dbContext.Model.FindEntityType(typeof(JobOccurrenceEntity))?.GetTableName()))
        {
            return;
        }

        dbContext.GetService<IRelationalDatabaseCreator>().CreateTables();
    }

    private static void ClearTables(TestJobSchedulerDbContext dbContext)
    {
        dbContext.JobBatchHistory.RemoveRange(dbContext.JobBatchHistory);
        dbContext.JobExecutionHistory.RemoveRange(dbContext.JobExecutionHistory);
        dbContext.JobExecutions.RemoveRange(dbContext.JobExecutions);
        dbContext.JobAcceptedEvents.RemoveRange(dbContext.JobAcceptedEvents);
        dbContext.JobBatchOccurrences.RemoveRange(dbContext.JobBatchOccurrences);
        dbContext.JobOccurrenceDependencies.RemoveRange(dbContext.JobOccurrenceDependencies);
        dbContext.JobLeases.RemoveRange(dbContext.JobLeases);
        dbContext.JobOccurrences.RemoveRange(dbContext.JobOccurrences);
        dbContext.JobTriggerRuntimeStates.RemoveRange(dbContext.JobTriggerRuntimeStates);
        dbContext.JobRuntimeStates.RemoveRange(dbContext.JobRuntimeStates);
        dbContext.JobBatches.RemoveRange(dbContext.JobBatches);
        dbContext.AppEntities.RemoveRange(dbContext.AppEntities);
        dbContext.SaveChanges();
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
                _ => throw new InvalidOperationException($"Unsupported provider '{providerName}' for jobs table checks.")
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

public sealed class TestJobSchedulerDbContext(DbContextOptions<TestJobSchedulerDbContext> options)
    : DbContext(options), IJobsContext
{
    public DbSet<TestAppEntity> AppEntities { get; set; }

    public DbSet<JobRuntimeStateEntity> JobRuntimeStates { get; set; }

    public DbSet<JobTriggerRuntimeStateEntity> JobTriggerRuntimeStates { get; set; }

    public DbSet<JobOccurrenceEntity> JobOccurrences { get; set; }

    public DbSet<JobOccurrenceDependencyEntity> JobOccurrenceDependencies { get; set; }

    public DbSet<JobBatchEntity> JobBatches { get; set; }

    public DbSet<JobBatchOccurrenceEntity> JobBatchOccurrences { get; set; }

    public DbSet<JobExecutionEntity> JobExecutions { get; set; }

    public DbSet<JobExecutionHistoryEntity> JobExecutionHistory { get; set; }

    public DbSet<JobBatchHistoryEntity> JobBatchHistory { get; set; }

    public DbSet<JobAcceptedEventEntity> JobAcceptedEvents { get; set; }

    public DbSet<JobLeaseEntity> JobLeases { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestAppEntity>().HasKey(x => x.Id);
        base.OnModelCreating(modelBuilder);
    }
}

public sealed class TestAppEntity
{
    public int Id { get; set; }

    public string Name { get; set; }
}

public sealed class SuccessJob : JobBase
{
    public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Success());
}

public sealed class FailJob : JobBase
{
    public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Failure().WithError(new ValidationError("failed")));
}

public sealed class TestTimeProvider(DateTimeOffset initialUtc) : TimeProvider
{
    private DateTimeOffset currentUtc = initialUtc;

    public override DateTimeOffset GetUtcNow() => this.currentUtc;

    public void Advance(TimeSpan value)
    {
        this.currentUtc = this.currentUtc.Add(value);
    }
}

public sealed class TestHostApplicationLifetime : IHostApplicationLifetime
{
    public CancellationToken ApplicationStarted => CancellationToken.None;

    public CancellationToken ApplicationStopping => CancellationToken.None;

    public CancellationToken ApplicationStopped => CancellationToken.None;

    public void StopApplication()
    {
    }
}
