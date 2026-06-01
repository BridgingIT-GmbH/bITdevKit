// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Jobs;

using System.Text;
using BridgingIT.DevKit.Application.Jobs;
using Microsoft.Extensions.DependencyInjection;

public class JobPersistenceStoreTests(ITestOutputHelper output) : JobSchedulerTestBase(output)
{
    [Fact]
    public async Task OccurrenceStore_CreateLoadAndUpdate_Succeeds()
    {
        // Arrange
        var sut = CreateProvider();
        var occurrence = CreateOccurrence();

        // Act
        var created = await sut.Occurrences.TryCreateAsync(occurrence);
        var loaded = await sut.Occurrences.GetAsync(occurrence.OccurrenceId);
        await sut.Occurrences.UpdateAsync(loaded with { Status = JobOccurrenceStatus.Due });
        var updated = await sut.Occurrences.GetAsync(occurrence.OccurrenceId);

        // Assert
        created.ShouldBeTrue();
        loaded.ShouldNotBeNull();
        loaded.Data.ShouldBeOfType<SamplePayload>().CustomerId.ShouldBe("C-1");
        updated.Status.ShouldBe(JobOccurrenceStatus.Due);
    }

    [Fact]
    public async Task OccurrenceStore_DeterministicOccurrenceDeduplication_PreventsDuplicates()
    {
        // Arrange
        var sut = CreateProvider();
        var occurrence = CreateOccurrence();

        // Act
        var first = await sut.Occurrences.TryCreateAsync(occurrence);
        var second = await sut.Occurrences.TryCreateAsync(occurrence with { OccurrenceId = Guid.NewGuid() });

        // Assert
        first.ShouldBeTrue();
        second.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecutionHistoryStore_AppendBehavior_PreservesOrder()
    {
        // Arrange
        var sut = CreateProvider();
        var occurrence = CreateOccurrence();
        await sut.Occurrences.TryCreateAsync(occurrence);

        var first = CreateHistoryEntry(occurrence.OccurrenceId, null, "Started", DateTimeOffset.UtcNow.AddMinutes(-1));
        var second = CreateHistoryEntry(occurrence.OccurrenceId, null, "Completed", DateTimeOffset.UtcNow);

        // Act
        await sut.ExecutionHistory.AppendAsync(first);
        await sut.ExecutionHistory.AppendAsync(second);
        var items = await sut.ExecutionHistory.ListAsync(occurrence.OccurrenceId);

        // Assert
        items.Count.ShouldBe(2);
        items[0].EventName.ShouldBe("Started");
        items[1].EventName.ShouldBe("Completed");
    }

    [Fact]
    public async Task PreviousExecutionStore_FindsPreviousExecution()
    {
        // Arrange
        var sut = CreateProvider();
        var first = CreateExecution(1, JobExecutionStatus.Failed, DateTimeOffset.UtcNow.AddMinutes(-5));
        var second = CreateExecution(2, JobExecutionStatus.Started, DateTimeOffset.UtcNow.AddMinutes(-1), first.OccurrenceId);
        await sut.Executions.CreateAsync(first);
        await sut.Executions.CreateAsync(second);

        // Act
        var previous = await sut.PreviousExecutions.GetPreviousExecutionAsync(second.OccurrenceId, second.ExecutionId);

        // Assert
        previous.ShouldNotBeNull();
        previous.ExecutionId.ShouldBe(first.ExecutionId);
    }

    [Fact]
    public async Task PreviousExecutionStore_FindsPreviousSuccessfulExecution()
    {
        // Arrange
        var sut = CreateProvider();
        var earlier = CreateExecution(1, JobExecutionStatus.Completed, DateTimeOffset.UtcNow.AddMinutes(-20), completedUtc: DateTimeOffset.UtcNow.AddMinutes(-19));
        var later = CreateExecution(2, JobExecutionStatus.Failed, DateTimeOffset.UtcNow.AddMinutes(-10), earlier.OccurrenceId);
        await sut.Executions.CreateAsync(earlier);
        await sut.Executions.CreateAsync(later);

        // Act
        var previous = await sut.PreviousExecutions.GetPreviousSuccessfulExecutionAsync(earlier.JobName, earlier.TriggerName, DateTimeOffset.UtcNow.AddMinutes(-5));

        // Assert
        previous.ShouldNotBeNull();
        previous.ExecutionId.ShouldBe(earlier.ExecutionId);
    }

    [Fact]
    public async Task DependencyStore_AddAndLookup_Succeeds()
    {
        // Arrange
        var sut = CreateProvider();
        var dependency = new JobOccurrenceDependency
        {
            DependencyId = Guid.NewGuid(),
            DependentOccurrenceId = Guid.NewGuid(),
            PrerequisiteOccurrenceId = Guid.NewGuid(),
            RequiredStatuses = [JobOccurrenceStatus.Completed],
            Status = JobDependencyStatus.Pending,
            FailurePolicy = JobDependencyFailurePolicy.KeepBlocked,
            CreatedDate = DateTimeOffset.UtcNow,
            UpdatedDate = DateTimeOffset.UtcNow,
        };

        // Act
        await sut.Dependencies.AddAsync(dependency);
        var byDependent = await sut.Dependencies.ListByDependentAsync(dependency.DependentOccurrenceId);
        var byPrerequisite = await sut.Dependencies.ListByPrerequisiteAsync(dependency.PrerequisiteOccurrenceId);

        // Assert
        byDependent.Count.ShouldBe(1);
        byPrerequisite.Count.ShouldBe(1);
        byDependent[0].DependencyId.ShouldBe(dependency.DependencyId);
        byPrerequisite[0].DependencyId.ShouldBe(dependency.DependencyId);
    }

    [Fact]
    public async Task DependencyStore_Update_ReplacesPersistedStatusAndReason()
    {
        // Arrange
        var sut = CreateProvider();
        var dependency = new JobOccurrenceDependency
        {
            DependencyId = Guid.NewGuid(),
            DependentOccurrenceId = Guid.NewGuid(),
            PrerequisiteOccurrenceId = Guid.NewGuid(),
            RequiredStatuses = [JobOccurrenceStatus.Completed],
            Status = JobDependencyStatus.Pending,
            FailurePolicy = JobDependencyFailurePolicy.KeepBlocked,
            Reason = "waiting",
            CreatedDate = DateTimeOffset.UtcNow,
            UpdatedDate = DateTimeOffset.UtcNow,
        };
        await sut.Dependencies.AddAsync(dependency);

        // Act
        await sut.Dependencies.UpdateAsync(dependency with
        {
            Status = JobDependencyStatus.Satisfied,
            Reason = "completed",
            UpdatedDate = DateTimeOffset.UtcNow.AddMinutes(1),
        });
        var loaded = (await sut.Dependencies.ListByDependentAsync(dependency.DependentOccurrenceId)).Single();

        // Assert
        loaded.Status.ShouldBe(JobDependencyStatus.Satisfied);
        loaded.Reason.ShouldBe("completed");
    }

    [Fact]
    public async Task BatchStore_CreatesBatchAndMembership()
    {
        // Arrange
        var sut = CreateProvider();
        var batchId = Guid.NewGuid();
        var batch = new JobBatch
        {
            BatchId = batchId,
            Description = "Reprocess customers",
            Status = JobBatchStatus.Created,
            CompletionPolicy = JobBatchCompletionPolicy.RequireAllSucceeded,
            CreatedDate = DateTimeOffset.UtcNow,
            UpdatedDate = DateTimeOffset.UtcNow,
        };
        var membership = new[]
        {
            new JobBatchOccurrence
            {
                BatchId = batchId,
                OccurrenceId = Guid.NewGuid(),
                ChildStatus = JobOccurrenceStatus.Materialized,
                Sequence = 1,
                CreatedDate = DateTimeOffset.UtcNow,
                UpdatedDate = DateTimeOffset.UtcNow,
            },
        };

        // Act
        var created = await sut.Batches.TryCreateAsync(batch, membership);
        var loadedBatch = await sut.Batches.GetAsync(batchId);
        var loadedMembership = await sut.Batches.ListOccurrencesAsync(batchId);

        // Assert
        created.ShouldBeTrue();
        loadedBatch.ShouldNotBeNull();
        loadedMembership.Count.ShouldBe(1);
        loadedMembership[0].BatchId.ShouldBe(batchId);
    }

    [Fact]
    public async Task InMemoryProvider_UsesSerializerForOccurrenceDataAndMetadataBoundaries()
    {
        // Arrange
        var serializer = new TrackingSerializer();
        var sut = new InMemoryJobStoreProvider(serializer);
        var occurrence = CreateOccurrence();

        // Act
        await sut.Occurrences.TryCreateAsync(occurrence);
        var loaded = await sut.Occurrences.GetAsync(occurrence.OccurrenceId);

        // Assert
        serializer.SerializeCalls.ShouldBeGreaterThanOrEqualTo(2);
        serializer.DeserializeCalls.ShouldBeGreaterThanOrEqualTo(2);
        loaded.Properties["source"].ShouldBe("unit-test");
    }

    [Fact]
    public async Task InMemoryProvider_DoesNotActAsAuthoritativeDefinitionStorage()
    {
        // Arrange
        var services = new ServiceCollection();
        this.ConfigureLogging(services);
        services.AddJobScheduler();
        var context = services.AddJobScheduler();
        context.WithJob<JobFoundationRegistrationTests.SampleJobAccessor>("cleanup", job => job
            .Description("Removes stale records.")
            .AddTrigger("manual", trigger => trigger.Manual()));
        using var provider = services.BuildServiceProvider();
        var registrations = provider.GetRequiredService<JobRegistrationStore>().GetDefinitions();
        var storeProvider = provider.GetRequiredService<IJobStoreProvider>();

        // Act
        var occurrences = await storeProvider.Queries.ListOccurrencesAsync();
        var runtimeStates = await storeProvider.RuntimeStates.ListAsync();

        // Assert
        registrations.Count.ShouldBe(1);
        occurrences.ShouldBeEmpty();
        runtimeStates.ShouldBeEmpty();
    }

    [Fact]
    public async Task LeaseStore_RenewWithWrongOwnershipToken_ReturnsNull()
    {
        // Arrange
        var sut = CreateProvider();
        var occurrenceId = Guid.NewGuid();
        var lease = await sut.Leases.TryAcquireAsync(occurrenceId, "scheduler-a", TimeSpan.FromMinutes(1));

        // Act
        var renewed = await sut.Leases.RenewAsync(occurrenceId, "scheduler-a", "wrong-token", TimeSpan.FromMinutes(1));

        // Assert
        lease.ShouldNotBeNull();
        renewed.ShouldBeNull();
        (await sut.Leases.GetAsync(occurrenceId)).ShouldNotBeNull();
    }

    [Fact]
    public async Task BatchStore_DuplicateCreate_IsRejected()
    {
        // Arrange
        var sut = CreateProvider();
        var batchId = Guid.NewGuid();
        var batch = new JobBatch
        {
            BatchId = batchId,
            Description = "Reprocess customers",
            Status = JobBatchStatus.Created,
            CompletionPolicy = JobBatchCompletionPolicy.RequireAllSucceeded,
            CreatedDate = DateTimeOffset.UtcNow,
            UpdatedDate = DateTimeOffset.UtcNow,
        };

        // Act
        var first = await sut.Batches.TryCreateAsync(batch, []);
        var second = await sut.Batches.TryCreateAsync(batch with { Description = "duplicate" }, []);

        // Assert
        first.ShouldBeTrue();
        second.ShouldBeFalse();
    }

    private InMemoryJobStoreProvider CreateProvider()
    {
        return new(new SystemTextJsonSerializer());
    }

    private static JobOccurrence CreateOccurrence()
    {
        return new JobOccurrence
        {
            OccurrenceId = Guid.NewGuid(),
            OccurrenceKey = "occ-key-001",
            JobName = "cleanup",
            TriggerName = "manual",
            TriggerType = JobTriggerType.Manual,
            Status = JobOccurrenceStatus.Materialized,
            DueUtc = new DateTimeOffset(2026, 05, 26, 12, 00, 00, TimeSpan.Zero),
            ScheduledUtc = null,
            Data = new SamplePayload("C-1"),
            DataType = typeof(SamplePayload),
            Properties = new PropertyBag { ["source"] = "unit-test" },
            CorrelationId = "corr-001",
            CausationId = "cause-001",
            IdempotencyKey = "idemp-001",
            CreatedDate = DateTimeOffset.UtcNow,
            UpdatedDate = DateTimeOffset.UtcNow,
        };
    }

    private static JobExecution CreateExecution(int attemptNumber, JobExecutionStatus status, DateTimeOffset startedUtc, Guid? occurrenceId = null, DateTimeOffset? completedUtc = null)
    {
        return new JobExecution
        {
            ExecutionId = Guid.NewGuid(),
            OccurrenceId = occurrenceId ?? Guid.NewGuid(),
            JobName = "cleanup",
            TriggerName = "manual",
            AttemptNumber = attemptNumber,
            Status = status,
            SchedulerInstanceId = "scheduler-1",
            StartedUtc = startedUtc,
            CompletedUtc = completedUtc,
            Message = status.ToString(),
            CreatedDate = startedUtc,
            UpdatedDate = completedUtc ?? startedUtc,
        };
    }

    private static JobExecutionHistoryEntry CreateHistoryEntry(Guid occurrenceId, Guid? executionId, string eventName, DateTimeOffset recordedAt)
    {
        return new JobExecutionHistoryEntry
        {
            HistoryId = Guid.NewGuid(),
            OccurrenceId = occurrenceId,
            ExecutionId = executionId,
            JobName = "cleanup",
            TriggerName = "manual",
            SchedulerInstanceId = "scheduler-1",
            EventName = eventName,
            RecordedAt = recordedAt,
        };
    }

    private sealed record SamplePayload(string CustomerId);

    private sealed class TrackingSerializer : ISerializer
    {
        private readonly SystemTextJsonSerializer inner = new();

        public int SerializeCalls { get; private set; }

        public int DeserializeCalls { get; private set; }

        public void Serialize(object value, Stream output)
        {
            this.SerializeCalls++;
            this.inner.Serialize(value, output);
        }

        public object Deserialize(Stream input, Type type)
        {
            this.DeserializeCalls++;
            return this.inner.Deserialize(input, type);
        }

        public T Deserialize<T>(Stream input)
        {
            this.DeserializeCalls++;
            return this.inner.Deserialize<T>(input);
        }
    }
}
