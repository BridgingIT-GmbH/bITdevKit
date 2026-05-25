// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Orchestrations;

using BridgingIT.DevKit.Application.Orchestrations;
using Microsoft.Extensions.DependencyInjection;

public class OrchestrationQueryServiceTests
{
    [Fact]
    public async Task GetAsync_AndGetContextAsync_WhenInstanceExists_ReturnPersistedModels()
    {
        // Arrange
        using var serviceProvider = CreateServices();
        var instances = serviceProvider.GetRequiredService<IOrchestrationInstanceStore>();
        var sut = serviceProvider.GetRequiredService<IOrchestrationQueryService>();
        var instanceId = Guid.NewGuid();
        var context = CreateContext(
            instanceId,
            orchestrationName: "OrderApproval",
            status: OrchestrationStatus.Waiting,
            currentState: "AwaitingApproval",
            correlationId: "corr-42",
            startedUtc: new DateTimeOffset(2026, 05, 07, 09, 00, 00, TimeSpan.Zero));

        context.Properties["priority"] = "high";

        await instances.CreateAsync(context, "order-42");

        // Act
        var instance = await sut.GetAsync(instanceId);
        var snapshot = await sut.GetContextAsync(instanceId);

        // Assert
        instance.IsSuccess.ShouldBeTrue();
        instance.Value.InstanceId.ShouldBe(instanceId);
        instance.Value.OrchestrationName.ShouldBe("OrderApproval");
        instance.Value.Status.ShouldBe(nameof(OrchestrationStatus.Waiting));
        instance.Value.CurrentState.ShouldBe("AwaitingApproval");
        instance.Value.CorrelationId.ShouldBe("corr-42");
        instance.Value.ConcurrencyKey.ShouldBe("order-42");

        snapshot.IsSuccess.ShouldBeTrue();
        snapshot.Value.InstanceId.ShouldBe(instanceId);
        snapshot.Value.ContextType.ShouldContain(nameof(QueryTestData));
        snapshot.Value.ContextJson.ShouldContain("priority");
        snapshot.Value.ContextJson.ShouldContain("orderId");
    }

    [Fact]
    public async Task QueryAsync_WhenFilteringAndPagingAreApplied_ReturnsPagedPersistedInstances()
    {
        // Arrange
        using var serviceProvider = CreateServices();
        var instances = serviceProvider.GetRequiredService<IOrchestrationInstanceStore>();
        var sut = serviceProvider.GetRequiredService<IOrchestrationQueryService>();
        var baseTime = new DateTimeOffset(2026, 05, 07, 08, 00, 00, TimeSpan.Zero);

        await instances.CreateAsync(CreateContext(Guid.NewGuid(), "OrderApproval", OrchestrationStatus.Waiting, "AwaitingApproval", "corr-1", baseTime), "orders");
        await instances.CreateAsync(CreateContext(Guid.NewGuid(), "OrderApproval", OrchestrationStatus.Paused, "NeedsReview", "corr-2", baseTime.AddMinutes(10)), "orders");
        await instances.CreateAsync(CreateContext(Guid.NewGuid(), "OrderApproval", OrchestrationStatus.Waiting, "AwaitingApproval", "corr-3", baseTime.AddMinutes(20)), "orders");
        await instances.CreateAsync(CreateContext(Guid.NewGuid(), "TelephoneCall", OrchestrationStatus.Waiting, "Ringing", "corr-4", baseTime.AddMinutes(30)), "calls");

        // Act
        var result = await sut.QueryAsync(new OrchestrationQueryRequest
        {
            OrchestrationName = "OrderApproval",
            Statuses = [nameof(OrchestrationStatus.Waiting), nameof(OrchestrationStatus.Paused)],
            States = ["AwaitingApproval", "NeedsReview"],
            ConcurrencyKey = "orders",
            Skip = 1,
            Take = 1,
            SortBy = "StartedUtc",
            SortDescending = false,
        });

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(3);
        result.CurrentPage.ShouldBe(2);
        result.PageSize.ShouldBe(1);
        result.Value.Count().ShouldBe(1);
        result.Value.Single().CorrelationId.ShouldBe("corr-2");
    }

    [Fact]
    public async Task GetHistoryAsync_WhenPersistedHistoryExists_ReturnsChronologicalHistoryModels()
    {
        // Arrange
        using var serviceProvider = CreateServices();
        var instances = serviceProvider.GetRequiredService<IOrchestrationInstanceStore>();
        var historyStore = serviceProvider.GetRequiredService<IOrchestrationHistoryStore>();
        var sut = serviceProvider.GetRequiredService<IOrchestrationQueryService>();
        var instanceId = Guid.NewGuid();

        await instances.CreateAsync(CreateContext(instanceId, "OrderApproval"), "orders");
        await historyStore.AppendAsync(new OrchestrationHistoryEntry
        {
            InstanceId = instanceId,
            EventType = "Waiting",
            StateName = "AwaitingApproval",
            Details = "waiting for manager",
            RecordedAt = new DateTimeOffset(2026, 05, 07, 10, 00, 00, TimeSpan.Zero),
            RecordedBy = "worker-1",
        });
        await historyStore.AppendAsync(new OrchestrationHistoryEntry
        {
            InstanceId = instanceId,
            EventType = "SignalReceived",
            StateName = "AwaitingApproval",
            Details = "{\"signal\":\"Approved\"}",
            RecordedAt = new DateTimeOffset(2026, 05, 07, 10, 01, 00, TimeSpan.Zero),
            RecordedBy = "worker-2",
        });

        // Act
        var result = await sut.GetHistoryAsync(instanceId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(2);
        result.Value[0].EventType.ShouldBe("Waiting");
        result.Value[0].Message.ShouldBe("waiting for manager");
        result.Value[1].EventType.ShouldBe("SignalReceived");
        result.Value[1].DataJson.ShouldBe("{\"signal\":\"Approved\"}");
    }

    [Fact]
    public async Task GetSignalsAsync_WhenPersistedSignalsExist_ReturnsSignalModels()
    {
        // Arrange
        using var serviceProvider = CreateServices();
        var instances = serviceProvider.GetRequiredService<IOrchestrationInstanceStore>();
        var signals = serviceProvider.GetRequiredService<IOrchestrationSignalStore>();
        var sut = serviceProvider.GetRequiredService<IOrchestrationQueryService>();
        var instanceId = Guid.NewGuid();

        await instances.CreateAsync(CreateContext(instanceId, "OrderApproval"), "orders");
        var first = await signals.PersistAsync(instanceId, "Approved", new QueryApprovalSignal { ApprovedBy = "manager-1" }, "AwaitingApproval", "sig-1");
        await signals.UpdateStatusAsync(first.SignalId, OrchestrationSignalStatus.Processed, "accepted");
        await signals.PersistAsync(instanceId, "Reminder", new QueryApprovalSignal { ApprovedBy = "system" }, idempotencyKey: "sig-2");

        // Act
        var result = await sut.GetSignalsAsync(instanceId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(2);
        result.Value[0].SignalName.ShouldBe("Approved");
        result.Value[0].ProcessingStatus.ShouldBe(nameof(OrchestrationSignalStatus.Processed));
        result.Value[0].PayloadJson.ShouldContain("manager-1");
        result.Value[1].SignalName.ShouldBe("Reminder");
    }

    [Fact]
    public async Task GetTimersAsync_WhenPersistedTimersExist_ReturnsTimerModels()
    {
        // Arrange
        using var serviceProvider = CreateServices();
        var instances = serviceProvider.GetRequiredService<IOrchestrationInstanceStore>();
        var timers = serviceProvider.GetRequiredService<IOrchestrationTimerStore>();
        var sut = serviceProvider.GetRequiredService<IOrchestrationQueryService>();
        var instanceId = Guid.NewGuid();
        var baseTime = new DateTimeOffset(2026, 05, 07, 11, 00, 00, TimeSpan.Zero);

        await instances.CreateAsync(CreateContext(instanceId, "OrderApproval"), "orders");
        var first = await timers.ScheduleAsync(instanceId, "WaitDelay", baseTime.AddMinutes(5), continuation: "resume-1");
        var second = await timers.ScheduleAsync(instanceId, "StateTimeout", baseTime.AddMinutes(10), targetState: "Escalated", continuation: "resume-2");
        await timers.UpdateStatusAsync(second.TimerId, OrchestrationTimerStatus.Consumed, "fired");

        // Act
        var result = await sut.GetTimersAsync(instanceId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(2);
        result.Value[0].Id.ShouldBe(first.TimerId);
        result.Value[0].TimerKind.ShouldBe("WaitDelay");
        result.Value[1].Id.ShouldBe(second.TimerId);
        result.Value[1].ProcessingStatus.ShouldBe(nameof(OrchestrationTimerStatus.Consumed));
        result.Value[1].MetadataJson.ShouldContain("Escalated");
    }

    [Fact]
    public async Task GetMetricsAsync_WhenFiltered_ReturnsCorrectAggregatesFromPersistedState()
    {
        // Arrange
        using var serviceProvider = CreateServices();
        var instances = serviceProvider.GetRequiredService<IOrchestrationInstanceStore>();
        var sut = serviceProvider.GetRequiredService<IOrchestrationQueryService>();
        var baseTime = new DateTimeOffset(2026, 05, 07, 07, 00, 00, TimeSpan.Zero);

        await instances.CreateAsync(CreateContext(Guid.NewGuid(), "OrderApproval", OrchestrationStatus.Waiting, "AwaitingApproval", "corr-1", baseTime), "orders");
        await instances.CreateAsync(CreateCompletedContext(Guid.NewGuid(), "OrderApproval", "Completed", "corr-2", baseTime.AddMinutes(10), baseTime.AddMinutes(20)), "orders");
        await instances.CreateAsync(CreateCompletedContext(Guid.NewGuid(), "OrderApproval", OrchestrationStatus.Failed, "FailedReview", "corr-3", baseTime.AddMinutes(30), baseTime.AddMinutes(40)), "orders");
        await instances.CreateAsync(CreateCompletedContext(Guid.NewGuid(), "TelephoneCall", OrchestrationStatus.Completed, "Done", "corr-4", baseTime.AddMinutes(50), baseTime.AddMinutes(55)), "calls");

        // Act
        var result = await sut.GetMetricsAsync(new OrchestrationMetricsRequest
        {
            OrchestrationName = "OrderApproval",
            States = ["AwaitingApproval", "Completed", "FailedReview"],
        });

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalCount.ShouldBe(3);
        result.Value.WaitingCount.ShouldBe(1);
        result.Value.CompletedCount.ShouldBe(1);
        result.Value.FailedCount.ShouldBe(1);
        result.Value.CountsByOrchestration["OrderApproval"].ShouldBe(3);
        result.Value.CountsByState["AwaitingApproval"].ShouldBe(1);
        result.Value.CountsByState["Completed"].ShouldBe(1);
        result.Value.CountsByState["FailedReview"].ShouldBe(1);
        result.Value.OldestWaitingStartedUtc.ShouldBe(baseTime);
        result.Value.AverageDurationSeconds.ShouldNotBeNull();
        result.Value.AverageDurationSeconds.Value.ShouldBe(600d, 0.001d);
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddOrchestrations();

        return services.BuildServiceProvider();
    }

    private static OrchestrationContext<QueryTestData> CreateContext(
        Guid instanceId,
        string orchestrationName,
        OrchestrationStatus status = OrchestrationStatus.Waiting,
        string currentState = "AwaitingApproval",
        string correlationId = null,
        DateTimeOffset? startedUtc = null)
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var context = new OrchestrationContext<QueryTestData>(
            orchestrationName,
            new QueryTestData { OrderId = $"ORD-{instanceId:N}" },
            services,
            instanceId,
            correlationId,
            startedUtc ?? new DateTimeOffset(2026, 05, 07, 09, 00, 00, TimeSpan.Zero))
        {
            Status = status,
            CurrentState = currentState,
            CurrentActivity = status == OrchestrationStatus.Completed ? null : "Advance",
            LastOutcome = OrchestrationOutcome.Wait("waiting"),
        };

        return context;
    }

    private static OrchestrationContext<QueryTestData> CreateCompletedContext(
        Guid instanceId,
        string orchestrationName,
        string currentState,
        string correlationId,
        DateTimeOffset startedUtc,
        DateTimeOffset completedUtc)
    {
        return CreateCompletedContext(instanceId, orchestrationName, OrchestrationStatus.Completed, currentState, correlationId, startedUtc, completedUtc);
    }

    private static OrchestrationContext<QueryTestData> CreateCompletedContext(
        Guid instanceId,
        string orchestrationName,
        OrchestrationStatus status,
        string currentState,
        string correlationId,
        DateTimeOffset startedUtc,
        DateTimeOffset completedUtc)
    {
        var context = CreateContext(instanceId, orchestrationName, status, currentState, correlationId, startedUtc);
        context.CompletedUtc = completedUtc;
        context.CurrentActivity = null;
        context.LastOutcome = OrchestrationOutcome.Complete("done");

        return context;
    }

    private class QueryTestData : IOrchestrationData
    {
        public string OrderId { get; set; }
    }

    private class QueryApprovalSignal
    {
        public string ApprovedBy { get; set; }
    }
}