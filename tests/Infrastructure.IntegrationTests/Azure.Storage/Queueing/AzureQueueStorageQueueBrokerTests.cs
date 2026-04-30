// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.Azure.Storage;

using BridgingIT.DevKit.Application.IntegrationTests.Queueing;
using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Infrastructure.Azure;
using Microsoft.Extensions.DependencyInjection;

[Collection(nameof(TestEnvironmentCollection))]
[IntegrationTest("Infrastructure")]
public class AzureQueueStorageQueueBrokerTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
{
    private readonly TestEnvironmentFixture fixture = fixture.WithOutput(output);

    [Fact]
    public async Task Broker_WhenSubscribed_CreatesQueueAndProcessesMessage()
    {
        AzureQueueStorageTestMessageHandler.Reset();
        var services = QueueingBrokerTestSupport.CreateServices();
        var queueSuffix = $"-{Guid.NewGuid():N}";

        services.AddQueueing()
            .WithSubscription<AzureQueueStorageTestMessage, AzureQueueStorageTestMessageHandler>()
            .WithAzureQueueStorageBroker(o => o
                .ConnectionString(this.fixture.AzuriteConnectionString)
                .QueueNameSuffix(queueSuffix)
                .VisibilityTimeout(TimeSpan.FromSeconds(5))
                .PollingInterval(TimeSpan.FromMilliseconds(100))
                .ProcessDelay(0));

        await using var provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<IQueueBroker>();
        var brokerService = provider.GetRequiredService<IQueueBrokerService>();

        await broker.Subscribe<AzureQueueStorageTestMessage, AzureQueueStorageTestMessageHandler>();
        await Task.Delay(500); // give poller time to start

        var message = new AzureQueueStorageTestMessage("integration-test");
        await broker.Enqueue(message);

        var processed = await QueueingBrokerTestSupport.WaitForAsync(
            () => Task.FromResult(AzureQueueStorageTestMessageHandler.LastMessageId == message.MessageId),
            attempts: 240,
            delayMilliseconds: 50);

        processed.ShouldBeTrue($"Message {message.MessageId} was not processed within the timeout");
    }

    [Fact]
    public async Task Broker_WhenHandlerNotRegistered_MessageWaitsForHandler()
    {
        var services = QueueingBrokerTestSupport.CreateServices();
        var queueSuffix = $"-{Guid.NewGuid():N}";

        services.AddQueueing()
            .WithSubscription<AzureQueueStorageTestMessage, AzureQueueStorageTestMessageHandler>()
            .WithAzureQueueStorageBroker(o => o
                .ConnectionString(this.fixture.AzuriteConnectionString)
                .QueueNameSuffix(queueSuffix)
                .VisibilityTimeout(TimeSpan.FromSeconds(5))
                .PollingInterval(TimeSpan.FromMilliseconds(100))
                .ProcessDelay(0));

        await using var provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<IQueueBroker>();
        var brokerService = provider.GetRequiredService<IQueueBrokerService>();

        var message = new AzureQueueStorageTestMessage("waiting-test");
        await broker.Enqueue(message);

        // Give the broker a moment to track the message
        await Task.Delay(500);

        var summary = await brokerService.GetSummaryAsync();
        summary.Total.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Broker_EnqueueBeforeSubscription_WaitsThenProcessesAfterSubscription()
    {
        AzureQueueStorageBeforeSubMessageHandler.Reset();
        var services = QueueingBrokerTestSupport.CreateServices();
        var queueSuffix = $"-{Guid.NewGuid():N}";

        services.AddQueueing()
            .WithSubscription<AzureQueueStorageBeforeSubMessage, AzureQueueStorageBeforeSubMessageHandler>()
            .WithAzureQueueStorageBroker(o => o
                .ConnectionString(this.fixture.AzuriteConnectionString)
                .QueueNameSuffix(queueSuffix)
                .VisibilityTimeout(TimeSpan.FromSeconds(5))
                .PollingInterval(TimeSpan.FromMilliseconds(100))
                .ProcessDelay(0));

        await using var provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<IQueueBroker>();
        var brokerService = provider.GetRequiredService<IQueueBrokerService>();

        var message = new AzureQueueStorageBeforeSubMessage("wait-for-handler");
        await broker.Enqueue(message);

        // Give the queue a moment to persist the message
        await Task.Delay(500);

        // Now subscribe - the poller should pick up the waiting message
        await broker.Subscribe<AzureQueueStorageBeforeSubMessage, AzureQueueStorageBeforeSubMessageHandler>();

        var processed = await QueueingBrokerTestSupport.WaitForAsync(
            () => Task.FromResult(AzureQueueStorageBeforeSubMessageHandler.Processed),
            attempts: 240,
            delayMilliseconds: 50);
        var summary = await brokerService.GetSummaryAsync();

        processed.ShouldBeTrue();
        AzureQueueStorageBeforeSubMessageHandler.LastProcessedMessageId.ShouldBe(message.MessageId);
        summary.Succeeded.ShouldBe(1);
    }

    [Fact]
    public async Task Broker_PauseResume_TracksStateAndContinuesProcessing()
    {
        AzureQueueStoragePauseMessageHandler.Reset();
        var services = QueueingBrokerTestSupport.CreateServices();
        var queueSuffix = $"-{Guid.NewGuid():N}";

        services.AddQueueing()
            .WithSubscription<AzureQueueStoragePauseMessage, AzureQueueStoragePauseMessageHandler>()
            .WithAzureQueueStorageBroker(o => o
                .ConnectionString(this.fixture.AzuriteConnectionString)
                .QueueNameSuffix(queueSuffix)
                .VisibilityTimeout(TimeSpan.FromSeconds(5))
                .PollingInterval(TimeSpan.FromMilliseconds(100))
                .ProcessDelay(0));

        await using var provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<IQueueBroker>();
        var brokerService = provider.GetRequiredService<IQueueBrokerService>();

        await broker.Subscribe<AzureQueueStoragePauseMessage, AzureQueueStoragePauseMessageHandler>();
        await Task.Delay(500);

        var queueName = typeof(AzureQueueStoragePauseMessage).PrettyName(false).ToLowerInvariant() + queueSuffix;

        // Verify baseline processing works
        var baselineMessage = new AzureQueueStoragePauseMessage("baseline");
        await broker.Enqueue(baselineMessage);

        var baselineProcessed = await QueueingBrokerTestSupport.WaitForAsync(
            () => Task.FromResult(AzureQueueStoragePauseMessageHandler.Processed),
            attempts: 240,
            delayMilliseconds: 50);
        baselineProcessed.ShouldBeTrue();

        AzureQueueStoragePauseMessageHandler.Reset();

        // Pause the queue
        await brokerService.PauseQueueAsync(queueName);
        var pausedSummary = await brokerService.GetSummaryAsync();
        pausedSummary.PausedQueues.ShouldContain(queueName);

        // Resume the queue
        await brokerService.ResumeQueueAsync(queueName);
        var resumedSummary = await brokerService.GetSummaryAsync();
        resumedSummary.PausedQueues.ShouldNotContain(queueName);

        // Verify processing still works after resume
        var afterResumeMessage = new AzureQueueStoragePauseMessage("after-resume");
        await broker.Enqueue(afterResumeMessage);

        var afterResumeProcessed = await QueueingBrokerTestSupport.WaitForAsync(
            () => Task.FromResult(AzureQueueStoragePauseMessageHandler.Processed),
            attempts: 240,
            delayMilliseconds: 50);
        afterResumeProcessed.ShouldBeTrue();
    }

    [Fact]
    public async Task Broker_WhenHandlerThrows_MessageIsRetriedThenDeadLettered()
    {
        AzureQueueStorageFailMessageHandler.Reset();
        var services = QueueingBrokerTestSupport.CreateServices();
        var queueSuffix = $"-{Guid.NewGuid():N}";

        services.AddQueueing()
            .WithSubscription<AzureQueueStorageFailMessage, AzureQueueStorageFailMessageHandler>()
            .WithAzureQueueStorageBroker(o => o
                .ConnectionString(this.fixture.AzuriteConnectionString)
                .QueueNameSuffix(queueSuffix)
                .VisibilityTimeout(TimeSpan.FromSeconds(5))
                .PollingInterval(TimeSpan.FromMilliseconds(100))
                .RetryDelay(TimeSpan.FromMilliseconds(500))
                .MaxDeliveryAttempts(2)
                .ProcessDelay(0));

        await using var provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<IQueueBroker>();
        var brokerService = provider.GetRequiredService<IQueueBrokerService>();

        await broker.Subscribe<AzureQueueStorageFailMessage, AzureQueueStorageFailMessageHandler>();
        await Task.Delay(500);

        var message = new AzureQueueStorageFailMessage("fail-me");
        await broker.Enqueue(message);

        // Wait for the message to be retried and eventually dead-lettered
        var deadLettered = await QueueingBrokerTestSupport.WaitForAsync(
            async () =>
            {
                var items = await brokerService.GetMessagesAsync();
                var tracked = items.FirstOrDefault(i => i.MessageId == message.MessageId);
                return tracked is not null && tracked.AttemptCount >= 2;
            },
            attempts: 120,
            delayMilliseconds: 250);

        deadLettered.ShouldBeTrue();
        AzureQueueStorageFailMessageHandler.AttemptCount.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Broker_GetMessagesAsync_ReturnsTrackedMessages()
    {
        var services = QueueingBrokerTestSupport.CreateServices();
        var queueSuffix = $"-{Guid.NewGuid():N}";

        services.AddQueueing()
            .WithSubscription<AzureQueueStorageTrackMessage, AzureQueueStorageTrackMessageHandler>()
            .WithAzureQueueStorageBroker(o => o
                .ConnectionString(this.fixture.AzuriteConnectionString)
                .QueueNameSuffix(queueSuffix)
                .VisibilityTimeout(TimeSpan.FromSeconds(5))
                .PollingInterval(TimeSpan.FromMilliseconds(100))
                .ProcessDelay(0));

        await using var provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<IQueueBroker>();
        var brokerService = provider.GetRequiredService<IQueueBrokerService>();

        var message = new AzureQueueStorageTrackMessage("track-me");
        await broker.Enqueue(message);

        var tracked = await QueueingBrokerTestSupport.WaitForAsync(async () =>
        {
            var items = await brokerService.GetMessagesAsync();
            return items.Any(i => i.MessageId == message.MessageId);
        });

        tracked.ShouldBeTrue();
    }

    [Fact]
    public async Task Broker_PurgeMessagesAsync_RemovesTrackedMessages()
    {
        var services = QueueingBrokerTestSupport.CreateServices();
        var queueSuffix = $"-{Guid.NewGuid():N}";

        services.AddQueueing()
            .WithSubscription<AzureQueueStorageTrackMessage, AzureQueueStorageTrackMessageHandler>()
            .WithAzureQueueStorageBroker(o => o
                .ConnectionString(this.fixture.AzuriteConnectionString)
                .QueueNameSuffix(queueSuffix)
                .VisibilityTimeout(TimeSpan.FromSeconds(5))
                .PollingInterval(TimeSpan.FromMilliseconds(100))
                .ProcessDelay(0));

        await using var provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<IQueueBroker>();
        var brokerService = provider.GetRequiredService<IQueueBrokerService>();

        var message = new AzureQueueStorageTrackMessage("purge-me");
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
    public async Task Broker_MultipleMessageTypes_OnlyCorrectHandlerTriggered()
    {
        AzureQueueStorageTestMessageHandler.Reset();
        AzureQueueStorageOtherMessageHandler.Reset();
        var services = QueueingBrokerTestSupport.CreateServices();
        var queueSuffix = $"-{Guid.NewGuid():N}";

        services.AddQueueing()
            .WithSubscription<AzureQueueStorageTestMessage, AzureQueueStorageTestMessageHandler>()
            .WithSubscription<AzureQueueStorageOtherMessage, AzureQueueStorageOtherMessageHandler>()
            .WithAzureQueueStorageBroker(o => o
                .ConnectionString(this.fixture.AzuriteConnectionString)
                .QueueNameSuffix(queueSuffix)
                .VisibilityTimeout(TimeSpan.FromSeconds(5))
                .PollingInterval(TimeSpan.FromMilliseconds(100))
                .ProcessDelay(0));

        await using var provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<IQueueBroker>();
        var brokerService = provider.GetRequiredService<IQueueBrokerService>();

        await broker.Subscribe<AzureQueueStorageTestMessage, AzureQueueStorageTestMessageHandler>();
        await broker.Subscribe<AzureQueueStorageOtherMessage, AzureQueueStorageOtherMessageHandler>();
        await Task.Delay(500);

        var messageA = new AzureQueueStorageTestMessage("type-a");
        var messageB = new AzureQueueStorageOtherMessage("type-b");
        await broker.Enqueue(messageA);
        await broker.Enqueue(messageB);

        var processed = await QueueingBrokerTestSupport.WaitForAsync(
            () => Task.FromResult(
                AzureQueueStorageTestMessageHandler.ProcessedIds.Contains(messageA.MessageId) &&
                AzureQueueStorageOtherMessageHandler.ProcessedIds.Contains(messageB.MessageId)),
            attempts: 240,
            delayMilliseconds: 50);

        processed.ShouldBeTrue();
        AzureQueueStorageTestMessageHandler.ProcessedIds.ShouldContain(messageA.MessageId);
        AzureQueueStorageTestMessageHandler.ProcessedIds.ShouldNotContain(messageB.MessageId);
        AzureQueueStorageOtherMessageHandler.ProcessedIds.ShouldContain(messageB.MessageId);
        AzureQueueStorageOtherMessageHandler.ProcessedIds.ShouldNotContain(messageA.MessageId);

        var summary = await brokerService.GetSummaryAsync();
        summary.Succeeded.ShouldBe(2);
    }
}

public sealed class AzureQueueStorageTestMessage(string value) : QueueMessageBase
{
    public string Value { get; } = value;
}

public sealed class AzureQueueStorageTestMessageHandler : IQueueMessageHandler<AzureQueueStorageTestMessage>
{
    private static readonly List<string> ProcessedIdsList = [];

    public static string LastMessageId { get; private set; }

    public static bool Processed { get; private set; }

    public static IReadOnlyList<string> ProcessedIds => ProcessedIdsList.AsReadOnly();

    public static void Reset()
    {
        Processed = false;
        LastMessageId = null;
        ProcessedIdsList.Clear();
    }

    public Task Handle(AzureQueueStorageTestMessage message, CancellationToken cancellationToken)
    {
        Processed = true;
        LastMessageId = message.MessageId;
        ProcessedIdsList.Add(message.MessageId);
        return Task.CompletedTask;
    }
}

public sealed class AzureQueueStorageBeforeSubMessage(string value) : QueueMessageBase
{
    public string Value { get; } = value;
}

public sealed class AzureQueueStorageBeforeSubMessageHandler : IQueueMessageHandler<AzureQueueStorageBeforeSubMessage>
{
    public static bool Processed { get; private set; }

    public static string LastProcessedMessageId { get; private set; }

    public static void Reset()
    {
        Processed = false;
        LastProcessedMessageId = null;
    }

    public Task Handle(AzureQueueStorageBeforeSubMessage message, CancellationToken cancellationToken)
    {
        Processed = true;
        LastProcessedMessageId = message.MessageId;
        return Task.CompletedTask;
    }
}

public sealed class AzureQueueStoragePauseMessage(string value) : QueueMessageBase
{
    public string Value { get; } = value;
}

public sealed class AzureQueueStoragePauseMessageHandler : IQueueMessageHandler<AzureQueueStoragePauseMessage>
{
    public static bool Processed { get; private set; }

    public static void Reset()
    {
        Processed = false;
    }

    public Task Handle(AzureQueueStoragePauseMessage message, CancellationToken cancellationToken)
    {
        Processed = true;
        return Task.CompletedTask;
    }
}

public sealed class AzureQueueStorageFailMessage(string value) : QueueMessageBase
{
    public string Value { get; } = value;
}

public sealed class AzureQueueStorageFailMessageHandler : IQueueMessageHandler<AzureQueueStorageFailMessage>
{
    public static int AttemptCount { get; private set; }

    public static void Reset()
    {
        AttemptCount = 0;
    }

    public Task Handle(AzureQueueStorageFailMessage message, CancellationToken cancellationToken)
    {
        AttemptCount++;
        throw new InvalidOperationException($"Simulated failure for message {message.MessageId}");
    }
}

public sealed class AzureQueueStorageTrackMessage(string value) : QueueMessageBase
{
    public string Value { get; } = value;
}

public sealed class AzureQueueStorageTrackMessageHandler : IQueueMessageHandler<AzureQueueStorageTrackMessage>
{
    public Task Handle(AzureQueueStorageTrackMessage message, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

public sealed class AzureQueueStorageOtherMessage(string value) : QueueMessageBase
{
    public string Value { get; } = value;
}

public sealed class AzureQueueStorageOtherMessageHandler : IQueueMessageHandler<AzureQueueStorageOtherMessage>
{
    private static readonly List<string> ProcessedIdsList = [];

    public static IReadOnlyList<string> ProcessedIds => ProcessedIdsList.AsReadOnly();

    public static void Reset()
    {
        ProcessedIdsList.Clear();
    }

    public Task Handle(AzureQueueStorageOtherMessage message, CancellationToken cancellationToken)
    {
        ProcessedIdsList.Add(message.MessageId);
        return Task.CompletedTask;
    }
}
