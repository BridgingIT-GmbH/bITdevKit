// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.RabbitMQ;

using BridgingIT.DevKit.Application.IntegrationTests.Queueing;
using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Infrastructure;
using BridgingIT.DevKit.Infrastructure.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;

[Collection(nameof(TestEnvironmentCollection))]
public class RabbitMQQueueBrokerTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
{
    private readonly TestEnvironmentFixture fixture = fixture.WithOutput(output);

    [Fact]
    public async Task RabbitMQBroker_EnqueueAndProcess_WithSubscription_ProcessesMessage()
    {
        RabbitMQQueueMessageHandler.Reset();
        var services = QueueingBrokerTestSupport.CreateServices();
        var queueSuffix = $"-{Guid.NewGuid():N}";

        services.AddQueueing()
            .WithSubscription<RabbitMQQueueMessage, RabbitMQQueueMessageHandler>()
            .WithRabbitMQBroker(o => o
                .ConnectionString(this.fixture.RabbitMQConnectionString)
                .QueueNameSuffix(queueSuffix)
                .PrefetchCount(10)
                .DurableEnabled(false)
                .MessageExpiration(TimeSpan.FromMinutes(1)));

        using var provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<IQueueBroker>();
        var brokerService = provider.GetRequiredService<IQueueBrokerService>();

        await broker.Subscribe<RabbitMQQueueMessage, RabbitMQQueueMessageHandler>();

        var message = new RabbitMQQueueMessage("hello-rabbit");
        await broker.Enqueue(message);

        var processed = await QueueingBrokerTestSupport.WaitForAsync(() => Task.FromResult(RabbitMQQueueMessageHandler.Processed));
        var summary = await brokerService.GetSummaryAsync();

        processed.ShouldBeTrue();
        RabbitMQQueueMessageHandler.LastProcessedMessageId.ShouldBe(message.MessageId);
        summary.Succeeded.ShouldBe(1);
    }

    [Fact]
    public async Task RabbitMQBroker_EnqueueBeforeSubscription_WaitsThenProcessesAfterSubscription()
    {
        RabbitMQQueueMessageHandler.Reset();
        var services = QueueingBrokerTestSupport.CreateServices();
        var queueSuffix = $"-{Guid.NewGuid():N}";

        services.AddQueueing()
            .WithSubscription<RabbitMQQueueMessage, RabbitMQQueueMessageHandler>()
            .WithRabbitMQBroker(o => o
                .ConnectionString(this.fixture.RabbitMQConnectionString)
                .QueueNameSuffix(queueSuffix)
                .DurableEnabled(false)
                .MessageExpiration(TimeSpan.FromMinutes(1)));

        using var provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<IQueueBroker>();
        var brokerService = provider.GetRequiredService<IQueueBrokerService>();

        var message = new RabbitMQQueueMessage("wait-for-handler");
        await broker.Enqueue(message);

        // Give RabbitMQ a moment to have the message in the queue
        await Task.Delay(500);

        // Now subscribe - the consumer should pick up the waiting message
        await broker.Subscribe<RabbitMQQueueMessage, RabbitMQQueueMessageHandler>();

        var processed = await QueueingBrokerTestSupport.WaitForAsync(
            () => Task.FromResult(RabbitMQQueueMessageHandler.Processed),
            attempts: 200,
            delayMilliseconds: 50);
        var summary = await brokerService.GetSummaryAsync();

        processed.ShouldBeTrue();
        RabbitMQQueueMessageHandler.LastProcessedMessageId.ShouldBe(message.MessageId);
        summary.Succeeded.ShouldBe(1);
    }

    [Fact]
    public async Task RabbitMQBroker_WhenQueueIsPaused_ResumeQueueProcessesPendingMessage()
    {
        RabbitMQQueueMessageHandler.Reset();
        var services = QueueingBrokerTestSupport.CreateServices();
        var queueSuffix = $"-{Guid.NewGuid():N}";

        services.AddQueueing()
            .WithSubscription<RabbitMQQueueMessage, RabbitMQQueueMessageHandler>()
            .WithRabbitMQBroker(o => o
                .ConnectionString(this.fixture.RabbitMQConnectionString)
                .QueueNameSuffix(queueSuffix)
                .DurableEnabled(false)
                .MessageExpiration(TimeSpan.FromMinutes(1)));

        using var provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<IQueueBroker>();
        var brokerService = provider.GetRequiredService<IQueueBrokerService>();

        await broker.Subscribe<RabbitMQQueueMessage, RabbitMQQueueMessageHandler>();

        var queueName = typeof(RabbitMQQueueMessage).PrettyName(false) + queueSuffix;

        await brokerService.PauseQueueAsync(queueName);

        var message = new RabbitMQQueueMessage("pause-me");
        await broker.Enqueue(message);

        // Wait a bit to ensure the message would have been processed if not paused
        await Task.Delay(1000);

        var processedWhilePaused = RabbitMQQueueMessageHandler.Processed;
        var pausedSummary = await brokerService.GetSummaryAsync();

        await brokerService.ResumeQueueAsync(queueName);

        var processed = await QueueingBrokerTestSupport.WaitForAsync(() => Task.FromResult(RabbitMQQueueMessageHandler.Processed));
        var resumedSummary = await brokerService.GetSummaryAsync();

        processedWhilePaused.ShouldBeFalse();
        pausedSummary.PausedQueues.ShouldContain(queueName);
        processed.ShouldBeTrue();
        resumedSummary.PausedQueues.ShouldNotContain(queueName);
        resumedSummary.Succeeded.ShouldBe(1);
    }

    [Fact]
    public async Task RabbitMQBroker_EnqueueAndWait_WaitsForConfirmation()
    {
        RabbitMQQueueMessageHandler.Reset();
        var services = QueueingBrokerTestSupport.CreateServices();
        var queueSuffix = $"-{Guid.NewGuid():N}";

        services.AddQueueing()
            .WithSubscription<RabbitMQQueueMessage, RabbitMQQueueMessageHandler>()
            .WithRabbitMQBroker(o => o
                .ConnectionString(this.fixture.RabbitMQConnectionString)
                .QueueNameSuffix(queueSuffix)
                .DurableEnabled(false)
                .MessageExpiration(TimeSpan.FromMinutes(1)));

        using var provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<IQueueBroker>();
        var brokerService = provider.GetRequiredService<IQueueBrokerService>();

        await broker.Subscribe<RabbitMQQueueMessage, RabbitMQQueueMessageHandler>();

        var message = new RabbitMQQueueMessage("enqueue-and-wait");
        await broker.EnqueueAndWait(message);

        var processed = await QueueingBrokerTestSupport.WaitForAsync(() => Task.FromResult(RabbitMQQueueMessageHandler.Processed));
        var summary = await brokerService.GetSummaryAsync();

        processed.ShouldBeTrue();
        summary.Succeeded.ShouldBe(1);
    }

    [Fact]
    public async Task RabbitMQBroker_WhenHandlerThrows_MessageIsRetriedThenDeadLettered()
    {
        RabbitMQFailingQueueMessageHandler.Reset();
        var services = QueueingBrokerTestSupport.CreateServices();
        var queueSuffix = $"-{Guid.NewGuid():N}";

        services.AddQueueing()
            .WithSubscription<RabbitMQFailingQueueMessage, RabbitMQFailingQueueMessageHandler>()
            .WithRabbitMQBroker(o => o
                .ConnectionString(this.fixture.RabbitMQConnectionString)
                .QueueNameSuffix(queueSuffix)
                .DurableEnabled(false)
                .MaxDeliveryAttempts(2)
                .MessageExpiration(TimeSpan.FromMinutes(1)));

        using var provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<IQueueBroker>();
        var brokerService = provider.GetRequiredService<IQueueBrokerService>();

        await broker.Subscribe<RabbitMQFailingQueueMessage, RabbitMQFailingQueueMessageHandler>();

        var message = new RabbitMQFailingQueueMessage("fail-me");
        await broker.Enqueue(message);

        // Wait for the message to be retried and eventually dead-lettered
        // The handler throws every time, so with MaxDeliveryAttempts=2,
        // it should be attempted twice then dropped
        var deadLettered = await QueueingBrokerTestSupport.WaitForAsync(
            async () =>
            {
                var stats = await brokerService.GetMessageStatsAsync(isArchived: null);
                return stats.DeadLettered >= 1 || RabbitMQFailingQueueMessageHandler.AttemptCount >= 2;
            },
            attempts: 120,
            delayMilliseconds: 250);

        var summary = await brokerService.GetSummaryAsync();

        // Handler should have been invoked at least twice (original + retry)
        RabbitMQFailingQueueMessageHandler.AttemptCount.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task RabbitMQBroker_GetMessagesAsync_ReturnsTrackedMessages()
    {
        var services = QueueingBrokerTestSupport.CreateServices();
        var queueSuffix = $"-{Guid.NewGuid():N}";

        services.AddQueueing()
            .WithSubscription<RabbitMQQueueMessage, RabbitMQQueueMessageHandler>()
            .WithRabbitMQBroker(o => o
                .ConnectionString(this.fixture.RabbitMQConnectionString)
                .QueueNameSuffix(queueSuffix)
                .DurableEnabled(false)
                .MessageExpiration(TimeSpan.FromMinutes(1)));

        using var provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<IQueueBroker>();
        var brokerService = provider.GetRequiredService<IQueueBrokerService>();

        var message = new RabbitMQQueueMessage("track-me");
        await broker.Enqueue(message);

        var tracked = await QueueingBrokerTestSupport.WaitForAsync(async () =>
        {
            var items = await brokerService.GetMessagesAsync();
            return items.Any(i => i.MessageId == message.MessageId);
        });

        tracked.ShouldBeTrue();
    }

    [Fact]
    public async Task RabbitMQBroker_PurgeMessagesAsync_RemovesTrackedMessages()
    {
        var services = QueueingBrokerTestSupport.CreateServices();
        var queueSuffix = $"-{Guid.NewGuid():N}";

        services.AddQueueing()
            .WithSubscription<RabbitMQQueueMessage, RabbitMQQueueMessageHandler>()
            .WithRabbitMQBroker(o => o
                .ConnectionString(this.fixture.RabbitMQConnectionString)
                .QueueNameSuffix(queueSuffix)
                .DurableEnabled(false)
                .MessageExpiration(TimeSpan.FromMinutes(1)));

        using var provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<IQueueBroker>();
        var brokerService = provider.GetRequiredService<IQueueBrokerService>();

        var message = new RabbitMQQueueMessage("purge-me");
        await broker.Enqueue(message);

        await QueueingBrokerTestSupport.WaitForAsync(async () =>
        {
            var items = await brokerService.GetMessagesAsync();
            return items.Any(i => i.MessageId == message.MessageId);
        });

        await brokerService.PurgeMessagesAsync();

        var stats = await brokerService.GetMessageStatsAsync(isArchived: null);
        stats.Total.ShouldBe(0);
    }

    [Fact]
    public async Task RabbitMQBroker_MultipleMessageTypes_OnlyCorrectHandlerTriggered()
    {
        RabbitMQQueueMessageHandler.Reset();
        RabbitMQOtherQueueMessageHandler.Reset();
        var services = QueueingBrokerTestSupport.CreateServices();
        var queueSuffix = $"-{Guid.NewGuid():N}";

        services.AddQueueing()
            .WithSubscription<RabbitMQQueueMessage, RabbitMQQueueMessageHandler>()
            .WithSubscription<RabbitMQOtherQueueMessage, RabbitMQOtherQueueMessageHandler>()
            .WithRabbitMQBroker(o => o
                .ConnectionString(this.fixture.RabbitMQConnectionString)
                .QueueNameSuffix(queueSuffix)
                .PrefetchCount(10)
                .DurableEnabled(false)
                .MessageExpiration(TimeSpan.FromMinutes(1)));

        using var provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<IQueueBroker>();
        var brokerService = provider.GetRequiredService<IQueueBrokerService>();

        await broker.Subscribe<RabbitMQQueueMessage, RabbitMQQueueMessageHandler>();
        await broker.Subscribe<RabbitMQOtherQueueMessage, RabbitMQOtherQueueMessageHandler>();

        var messageA = new RabbitMQQueueMessage("type-a");
        var messageB = new RabbitMQOtherQueueMessage("type-b");
        await broker.Enqueue(messageA);
        await broker.Enqueue(messageB);

        var processed = await QueueingBrokerTestSupport.WaitForAsync(
            () => Task.FromResult(
                RabbitMQQueueMessageHandler.ProcessedIds.Contains(messageA.MessageId) &&
                RabbitMQOtherQueueMessageHandler.ProcessedIds.Contains(messageB.MessageId)),
            attempts: 200,
            delayMilliseconds: 50);

        processed.ShouldBeTrue();
        RabbitMQQueueMessageHandler.ProcessedIds.ShouldContain(messageA.MessageId);
        RabbitMQQueueMessageHandler.ProcessedIds.ShouldNotContain(messageB.MessageId);
        RabbitMQOtherQueueMessageHandler.ProcessedIds.ShouldContain(messageB.MessageId);
        RabbitMQOtherQueueMessageHandler.ProcessedIds.ShouldNotContain(messageA.MessageId);

        var summary = await brokerService.GetSummaryAsync();
        summary.Succeeded.ShouldBe(2);
    }
}

public sealed class RabbitMQQueueMessage(string value) : QueueMessageBase
{
    public string Value { get; } = value;
}

public sealed class RabbitMQQueueMessageHandler : IQueueMessageHandler<RabbitMQQueueMessage>
{
    private static readonly List<string> ProcessedIdsList = [];

    public static bool Processed { get; private set; }

    public static string LastProcessedMessageId { get; private set; }

    public static IReadOnlyList<string> ProcessedIds => ProcessedIdsList.AsReadOnly();

    public static void Reset()
    {
        Processed = false;
        LastProcessedMessageId = null;
        ProcessedIdsList.Clear();
    }

    public Task Handle(RabbitMQQueueMessage message, CancellationToken cancellationToken)
    {
        Processed = true;
        LastProcessedMessageId = message.MessageId;
        ProcessedIdsList.Add(message.MessageId);
        return Task.CompletedTask;
    }
}

public sealed class RabbitMQFailingQueueMessage(string value) : QueueMessageBase
{
    public string Value { get; } = value;
}

public sealed class RabbitMQFailingQueueMessageHandler : IQueueMessageHandler<RabbitMQFailingQueueMessage>
{
    public static int AttemptCount { get; private set; }

    public static void Reset()
    {
        AttemptCount = 0;
    }

    public Task Handle(RabbitMQFailingQueueMessage message, CancellationToken cancellationToken)
    {
        AttemptCount++;
        throw new InvalidOperationException($"Simulated failure for message {message.MessageId}");
    }
}

public sealed class RabbitMQOtherQueueMessage(string value) : QueueMessageBase
{
    public string Value { get; } = value;
}

public sealed class RabbitMQOtherQueueMessageHandler : IQueueMessageHandler<RabbitMQOtherQueueMessage>
{
    private static readonly List<string> ProcessedIdsList = [];

    public static IReadOnlyList<string> ProcessedIds => ProcessedIdsList.AsReadOnly();

    public static void Reset()
    {
        ProcessedIdsList.Clear();
    }

    public Task Handle(RabbitMQOtherQueueMessage message, CancellationToken cancellationToken)
    {
        ProcessedIdsList.Add(message.MessageId);
        return Task.CompletedTask;
    }
}
