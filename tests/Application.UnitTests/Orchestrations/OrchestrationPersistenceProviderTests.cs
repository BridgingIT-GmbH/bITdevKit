// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Orchestrations;

using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

public class OrchestrationPersistenceProviderTests(ITestOutputHelper output) : OrchestrationTestBase(output)
{
    [Fact]
    public async Task CreateAsync_WhenContextIsPersisted_StoresSnapshotAndRehydratesPersistedContext()
    {
        // Arrange
        var serializer = new TrackingSerializer();
        var serviceProvider = CreateServices(services =>
        {
            services.AddSingleton<ISerializer>(serializer);
            services.AddOrchestrations();
        });

        var sut = serviceProvider.GetRequiredService<IOrchestrationStorageProvider>();
        var context = CreateContext();

        // Act
        var snapshot = await sut.Instances.CreateAsync(context, "orders/42");
        var rehydrated = await sut.Queries.GetContextAsync<PersistenceData>(context.InstanceId);

        // Assert
        snapshot.InstanceId.ShouldBe(context.InstanceId);
        snapshot.OrchestrationName.ShouldBe("SampleOrchestration");
        snapshot.ConcurrencyKey.ShouldBe("orders/42");
        snapshot.SerializedContext.ShouldNotBeNullOrWhiteSpace();
        rehydrated.Status.ShouldBe(OrchestrationStatus.Waiting);
        rehydrated.CurrentState.ShouldBe("AwaitingApproval");
        rehydrated.CurrentActivity.ShouldBe("SendApprovalRequest");
        rehydrated.Data.Name.ShouldBe("alpha");
        rehydrated.Data.Attempts.ShouldBe(2);
        rehydrated.Properties["Message"].ShouldBe("hello");
        rehydrated.Properties["Retries"].ShouldBe(2);
        serializer.SerializeCalls.ShouldBeGreaterThan(0);
        serializer.DeserializeCalls.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task SaveAsync_WhenSnapshotVersionMatches_IncrementsVersionAndPersistsLatestState()
    {
        // Arrange
        var sut = new InMemoryOrchestrationStorageProvider();
        var context = CreateContext();
        var snapshot = await sut.Instances.CreateAsync(context, "orders/42");

        context.Status = OrchestrationStatus.Completed;
        context.CurrentState = "Done";
        context.CurrentActivity = null;
        context.CompletedUtc = DateTimeOffset.UtcNow;
        context.Data.Attempts = 3;

        // Act
        var saved = await sut.Instances.SaveAsync(snapshot, context);
        var loaded = await sut.Queries.GetInstanceAsync(context.InstanceId);

        // Assert
        saved.Version.ShouldBe(2);
        saved.CreatedDate.ShouldBe(snapshot.CreatedDate);
        loaded.Status.ShouldBe(OrchestrationStatus.Completed);
        loaded.CurrentState.ShouldBe("Done");
        loaded.ConcurrencyKey.ShouldBe("orders/42");
    }

    [Fact]
    public async Task SaveAsync_WhenSnapshotVersionIsStale_ThrowsInvalidOperationException()
    {
        // Arrange
        var sut = new InMemoryOrchestrationStorageProvider();
        var context = CreateContext();
        var snapshot = await sut.Instances.CreateAsync(context);

        context.Data.Attempts = 4;
        var current = await sut.Instances.SaveAsync(snapshot, context);

        context.Data.Attempts = 5;

        // Act
        var action = async () => await sut.Instances.SaveAsync(snapshot, context);

        // Assert
        current.Version.ShouldBe(2);
        await action.ShouldThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AppendAsync_WhenHistoryEntriesAreAdded_PreservesAppendOnlyOrder()
    {
        // Arrange
        var sut = new InMemoryOrchestrationStorageProvider();
        var instanceId = Guid.NewGuid();
        var first = new OrchestrationHistoryEntry
        {
            InstanceId = instanceId,
            EventType = "Created",
            RecordedAt = new DateTimeOffset(2026, 05, 07, 10, 00, 00, TimeSpan.Zero),
        };
        var second = new OrchestrationHistoryEntry
        {
            InstanceId = instanceId,
            EventType = "Transition",
            RecordedAt = new DateTimeOffset(2026, 05, 07, 10, 01, 00, TimeSpan.Zero),
        };

        // Act
        await sut.History.AppendAsync(first);
        await sut.History.AppendAsync(second);
        var history = await sut.Queries.GetHistoryAsync(instanceId);

        // Assert
        history.Count.ShouldBe(2);
        history.Select(x => x.EventType).ShouldBe(["Created", "Transition"]);
    }

    [Fact]
    public async Task PersistAsync_WhenDuplicateIdempotencyKeyIsUsed_ReusesSignalAndTracksStatus()
    {
        // Arrange
        var sut = new InMemoryOrchestrationStorageProvider();
        var instanceId = Guid.NewGuid();

        // Act
        var first = await sut.Signals.PersistAsync(instanceId, "Approved", new ApprovalSignal { ApprovedBy = "user-1" }, "AwaitingApproval", "signal-1");
        var duplicate = await sut.Signals.PersistAsync(instanceId, "Approved", new ApprovalSignal { ApprovedBy = "user-2" }, "AwaitingApproval", "signal-1");
        var pending = await sut.Signals.GetProcessableAsync(instanceId, "AwaitingApproval");
        var updated = await sut.Signals.UpdateStatusAsync(first.SignalId, OrchestrationSignalStatus.Processed, "handled");
        var afterProcessing = await sut.Signals.GetProcessableAsync(instanceId, "AwaitingApproval");

        // Assert
        duplicate.SignalId.ShouldBe(first.SignalId);
        pending.Count.ShouldBe(1);
        updated.Status.ShouldBe(OrchestrationSignalStatus.Processed);
        updated.ProcessedUtc.ShouldNotBeNull();
        afterProcessing.ShouldBeEmpty();
    }

    [Fact]
    public async Task ScheduleAsync_WhenDueTimersAreLoaded_ReturnsDeterministicOrderAndTracksStatus()
    {
        // Arrange
        var sut = new InMemoryOrchestrationStorageProvider();
        var instanceId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 05, 07, 12, 00, 00, TimeSpan.Zero);

        // Act
        var first = await sut.Timers.ScheduleAsync(instanceId, "timeout", now.AddMinutes(5), targetState: "TimedOut");
        var second = await sut.Timers.ScheduleAsync(instanceId, "retry", now.AddMinutes(1), continuation: "retry-1");
        var due = await sut.Timers.GetDueAsync(now.AddMinutes(10));
        var updated = await sut.Timers.UpdateStatusAsync(second.TimerId, OrchestrationTimerStatus.Consumed, "fired");
        var remainingDue = await sut.Timers.GetDueAsync(now.AddMinutes(10));

        // Assert
        due.Select(x => x.TimerId).ShouldBe([second.TimerId, first.TimerId]);
        updated.Status.ShouldBe(OrchestrationTimerStatus.Consumed);
        remainingDue.Select(x => x.TimerId).ShouldBe([first.TimerId]);
    }

    [Fact]
    public async Task QueryAsync_WhenPersistedStateExists_ReturnsFilteredInstancesAndMetrics()
    {
        // Arrange
        var sut = new InMemoryOrchestrationStorageProvider();
        var waiting = CreateContext(orchestrationName: "Orders", currentState: "AwaitingApproval", status: OrchestrationStatus.Waiting);
        var completed = CreateContext(orchestrationName: "Orders", currentState: "Done", status: OrchestrationStatus.Completed);
        completed.CompletedUtc = DateTimeOffset.UtcNow;

        await sut.Instances.CreateAsync(waiting, "orders");
        await sut.Instances.CreateAsync(completed, "orders");
        await sut.History.AppendAsync(new OrchestrationHistoryEntry { InstanceId = waiting.InstanceId, EventType = "Waiting" });
        await sut.Signals.PersistAsync(waiting.InstanceId, "Approved", new ApprovalSignal { ApprovedBy = "user-1" });
        await sut.Timers.ScheduleAsync(waiting.InstanceId, "timeout", DateTimeOffset.UtcNow.AddMinutes(15));

        // Act
        var query = await sut.Queries.QueryAsync(new OrchestrationInstanceQuery { Statuses = [OrchestrationStatus.Waiting] });
        var metrics = await sut.Queries.GetMetricsAsync();

        // Assert
        query.TotalCount.ShouldBe(1);
        query.Items.Single().InstanceId.ShouldBe(waiting.InstanceId);
        metrics.TotalInstances.ShouldBe(2);
        metrics.WaitingInstances.ShouldBe(1);
        metrics.CompletedInstances.ShouldBe(1);
        metrics.HistoryCount.ShouldBe(1);
        metrics.SignalCount.ShouldBe(1);
        metrics.TimerCount.ShouldBe(1);
        metrics.InstanceCountsByOrchestration["Orders"].ShouldBe(2);
    }

    private ServiceProvider CreateServices(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        this.ConfigureLogging(services);
        configure(services);

        return services.BuildServiceProvider();
    }

    private static OrchestrationContext<PersistenceData> CreateContext(
        string orchestrationName = "SampleOrchestration",
        string currentState = "AwaitingApproval",
        OrchestrationStatus status = OrchestrationStatus.Waiting)
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var context = new OrchestrationContext<PersistenceData>(
            orchestrationName,
            new PersistenceData { Name = "alpha", Attempts = 2 },
            services,
            Guid.NewGuid(),
            "corr-1",
            new DateTimeOffset(2026, 05, 07, 09, 00, 00, TimeSpan.Zero))
        {
            Status = status,
            CurrentState = currentState,
            CurrentActivity = status == OrchestrationStatus.Completed ? null : "SendApprovalRequest",
            LastOutcome = OrchestrationOutcome.Wait("waiting"),
            FailureReason = status == OrchestrationStatus.Waiting ? "waiting" : null,
        };

        context.Properties["Message"] = "hello";
        context.Properties["Retries"] = 2;

        return context;
    }

    private class PersistenceData : IOrchestrationData
    {
        public string Name { get; set; }

        public int Attempts { get; set; }
    }

    private class ApprovalSignal
    {
        public string ApprovedBy { get; set; }
    }

    private class TrackingSerializer : ISerializer
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