// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.Azure;

using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Infrastructure.Azure;
using Microsoft.Extensions.DependencyInjection;

[Collection(nameof(TestEnvironmentCollection))]
[IntegrationTest("Infrastructure")]
public class ServiceBusQueueBrokerTests
{
    private readonly TestEnvironmentFixture fixture;

    public ServiceBusQueueBrokerTests(TestEnvironmentFixture fixture)
    {
        this.fixture = fixture;
    }

    [SkippableFact]
    public async Task Broker_WhenSubscribed_CreatesQueueAndProcessesMessage()
    {
        Skip.IfNot(await this.fixture.WaitForServiceBusEmulatorReadyAsync(), "Service Bus emulator did not become ready within timeout");

        ServiceBusQueueTestMessageHandler.Reset();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new QueueingRegistrationStore());
        services.AddSingleton(new QueueBrokerControlState());

        var context = new QueueingBuilderContext(services);
        context.WithServiceBusBroker(o => o
            .ConnectionString(this.fixture.ServiceBusEmulatorConnectionString)
            .QueueNamePrefix("test-")
            .AutoCreateQueue(false)
            .MaxConcurrentCalls(1)
            .PrefetchCount(0)
            .MaxDeliveryAttempts(3)
            .ProcessDelay(0));

        var provider = services.BuildServiceProvider();
        await using var broker = provider.GetRequiredService<IQueueBroker>() as IAsyncDisposable;
        var queueBroker = provider.GetRequiredService<IQueueBroker>();

        await queueBroker.Subscribe<ServiceBusQueueTestMessage, ServiceBusQueueTestMessageHandler>();
        await Task.Delay(2000); // give processor time to establish receive link

        var message = new ServiceBusQueueTestMessage("integration-test");
        await queueBroker.Enqueue(message);

        var processed = await WaitForAsync(() => Task.FromResult(ServiceBusQueueTestMessageHandler.LastMessageId == message.MessageId), attempts: 240, delayMilliseconds: 250);

        processed.ShouldBeTrue($"Message {message.MessageId} was not processed within the timeout");
    }

    [SkippableFact]
    public async Task Broker_WhenHandlerNotRegistered_MessageWaitsForHandler()
    {
        Skip.IfNot(await this.fixture.WaitForServiceBusEmulatorReadyAsync(), "Service Bus emulator did not become ready within timeout");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new QueueingRegistrationStore());
        services.AddSingleton(new QueueBrokerControlState());

        var context = new QueueingBuilderContext(services);
        context.WithServiceBusBroker(o => o
            .ConnectionString(this.fixture.ServiceBusEmulatorConnectionString)
            .QueueNamePrefix("test-")
            .AutoCreateQueue(false)
            .MaxConcurrentCalls(1)
            .PrefetchCount(0)
            .ProcessDelay(0));

        var provider = services.BuildServiceProvider();
        await using var broker = provider.GetRequiredService<IQueueBroker>() as IAsyncDisposable;
        var queueBroker = provider.GetRequiredService<IQueueBroker>();
        var brokerService = provider.GetRequiredService<IQueueBrokerService>();

        var message = new ServiceBusQueueTestMessage("waiting-test");
        await queueBroker.Enqueue(message);

        // Give the broker a moment to track the message
        await Task.Delay(500);

        var summary = await brokerService.GetSummaryAsync();
        summary.Total.ShouldBeGreaterThanOrEqualTo(1);
    }

    [SkippableFact]
    public async Task Broker_EnqueueBeforeSubscription_WaitsThenProcessesAfterSubscription()
    {
        Skip.IfNot(await this.fixture.WaitForServiceBusEmulatorReadyAsync(), "Service Bus emulator did not become ready within timeout");

        ServiceBusQueueBeforeSubMessageHandler.Reset();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new QueueingRegistrationStore());
        services.AddSingleton(new QueueBrokerControlState());

        var context = new QueueingBuilderContext(services);
        context.WithServiceBusBroker(o => o
            .ConnectionString(this.fixture.ServiceBusEmulatorConnectionString)
            .QueueNamePrefix("test-")
            .AutoCreateQueue(false)
            .MaxConcurrentCalls(1)
            .PrefetchCount(0)
            .ProcessDelay(0));

        var provider = services.BuildServiceProvider();
        await using var broker = provider.GetRequiredService<IQueueBroker>() as IAsyncDisposable;
        var queueBroker = provider.GetRequiredService<IQueueBroker>();
        var brokerService = provider.GetRequiredService<IQueueBrokerService>();

        var message = new ServiceBusQueueBeforeSubMessage("wait-for-handler");
        await queueBroker.Enqueue(message);

        // Give Service Bus a moment to have the message in the queue
        await Task.Delay(500);

        // Now subscribe - the processor should pick up the waiting message
        await queueBroker.Subscribe<ServiceBusQueueBeforeSubMessage, ServiceBusQueueBeforeSubMessageHandler>();

        var processed = await WaitForAsync(
            () => Task.FromResult(ServiceBusQueueBeforeSubMessageHandler.Processed),
            attempts: 240,
            delayMilliseconds: 250);
        var summary = await brokerService.GetSummaryAsync();

        processed.ShouldBeTrue();
        ServiceBusQueueBeforeSubMessageHandler.LastProcessedMessageId.ShouldBe(message.MessageId);
        summary.Succeeded.ShouldBe(1);
    }

    [SkippableFact]
    public async Task Broker_PauseResume_TracksStateAndContinuesProcessing()
    {
        Skip.IfNot(await this.fixture.WaitForServiceBusEmulatorReadyAsync(), "Service Bus emulator did not become ready within timeout");

        ServiceBusQueuePauseMessageHandler.Reset();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new QueueingRegistrationStore());
        services.AddSingleton(new QueueBrokerControlState());

        var context = new QueueingBuilderContext(services);
        context.WithServiceBusBroker(o => o
            .ConnectionString(this.fixture.ServiceBusEmulatorConnectionString)
            .QueueNamePrefix("test-")
            .AutoCreateQueue(false)
            .MaxConcurrentCalls(1)
            .PrefetchCount(0)
            .ProcessDelay(0));

        var provider = services.BuildServiceProvider();
        await using var broker = provider.GetRequiredService<IQueueBroker>() as IAsyncDisposable;
        var queueBroker = provider.GetRequiredService<IQueueBroker>();
        var brokerService = provider.GetRequiredService<IQueueBrokerService>();

        await queueBroker.Subscribe<ServiceBusQueuePauseMessage, ServiceBusQueuePauseMessageHandler>();
        await Task.Delay(2000);

        var queueName = "test-servicebusqueuepausemessage";

        // Verify baseline processing works
        var baselineMessage = new ServiceBusQueuePauseMessage("baseline");
        await queueBroker.Enqueue(baselineMessage);

        var baselineProcessed = await WaitForAsync(() => Task.FromResult(ServiceBusQueuePauseMessageHandler.Processed));
        baselineProcessed.ShouldBeTrue();

        ServiceBusQueuePauseMessageHandler.Reset();

        // Pause the queue
        await brokerService.PauseQueueAsync(queueName);
        var pausedSummary = await brokerService.GetSummaryAsync();
        pausedSummary.PausedQueues.ShouldContain(queueName);

        // Resume the queue
        await brokerService.ResumeQueueAsync(queueName);
        var resumedSummary = await brokerService.GetSummaryAsync();
        resumedSummary.PausedQueues.ShouldNotContain(queueName);

        // Verify processing still works after resume
        var afterResumeMessage = new ServiceBusQueuePauseMessage("after-resume");
        await queueBroker.Enqueue(afterResumeMessage);

        var afterResumeProcessed = await WaitForAsync(() => Task.FromResult(ServiceBusQueuePauseMessageHandler.Processed));
        afterResumeProcessed.ShouldBeTrue();
    }

    [SkippableFact]
    public async Task Broker_WhenHandlerThrows_MessageIsRetriedThenDeadLettered()
    {
        Skip.IfNot(await this.fixture.WaitForServiceBusEmulatorReadyAsync(), "Service Bus emulator did not become ready within timeout");

        ServiceBusQueueFailMessageHandler.Reset();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new QueueingRegistrationStore());
        services.AddSingleton(new QueueBrokerControlState());

        var context = new QueueingBuilderContext(services);
        context.WithServiceBusBroker(o => o
            .ConnectionString(this.fixture.ServiceBusEmulatorConnectionString)
            .QueueNamePrefix("test-")
            .AutoCreateQueue(false)
            .MaxConcurrentCalls(1)
            .PrefetchCount(0)
            .MaxDeliveryAttempts(2)
            .ProcessDelay(0));

        var provider = services.BuildServiceProvider();
        await using var broker = provider.GetRequiredService<IQueueBroker>() as IAsyncDisposable;
        var queueBroker = provider.GetRequiredService<IQueueBroker>();
        var brokerService = provider.GetRequiredService<IQueueBrokerService>();

        await queueBroker.Subscribe<ServiceBusQueueFailMessage, ServiceBusQueueFailMessageHandler>();
        await Task.Delay(2000);

        var message = new ServiceBusQueueFailMessage("fail-me");
        await queueBroker.Enqueue(message);

        // Wait for the message to be retried and eventually dead-lettered
        var deadLettered = await WaitForAsync(
            async () =>
            {
                var items = await brokerService.GetMessagesAsync();
                var tracked = items.FirstOrDefault(i => i.MessageId == message.MessageId);
                return tracked is not null && tracked.AttemptCount >= 2;
            },
            attempts: 120,
            delayMilliseconds: 250);

        deadLettered.ShouldBeTrue();
        ServiceBusQueueFailMessageHandler.AttemptCount.ShouldBeGreaterThanOrEqualTo(2);
    }

    [SkippableFact]
    public async Task Broker_GetMessagesAsync_ReturnsTrackedMessages()
    {
        Skip.IfNot(await this.fixture.WaitForServiceBusEmulatorReadyAsync(), "Service Bus emulator did not become ready within timeout");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new QueueingRegistrationStore());
        services.AddSingleton(new QueueBrokerControlState());

        var context = new QueueingBuilderContext(services);
        context.WithServiceBusBroker(o => o
            .ConnectionString(this.fixture.ServiceBusEmulatorConnectionString)
            .QueueNamePrefix("test-")
            .AutoCreateQueue(false)
            .MaxConcurrentCalls(1)
            .PrefetchCount(0)
            .ProcessDelay(0));

        var provider = services.BuildServiceProvider();
        await using var broker = provider.GetRequiredService<IQueueBroker>() as IAsyncDisposable;
        var queueBroker = provider.GetRequiredService<IQueueBroker>();
        var brokerService = provider.GetRequiredService<IQueueBrokerService>();

        var message = new ServiceBusQueueTrackMessage("track-me");
        await queueBroker.Enqueue(message);

        var tracked = await WaitForAsync(async () =>
        {
            var items = await brokerService.GetMessagesAsync();
            return items.Any(i => i.MessageId == message.MessageId);
        });

        tracked.ShouldBeTrue();
    }

    [SkippableFact]
    public async Task Broker_PurgeMessagesAsync_RemovesTrackedMessages()
    {
        Skip.IfNot(await this.fixture.WaitForServiceBusEmulatorReadyAsync(), "Service Bus emulator did not become ready within timeout");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new QueueingRegistrationStore());
        services.AddSingleton(new QueueBrokerControlState());

        var context = new QueueingBuilderContext(services);
        context.WithServiceBusBroker(o => o
            .ConnectionString(this.fixture.ServiceBusEmulatorConnectionString)
            .QueueNamePrefix("test-")
            .AutoCreateQueue(false)
            .MaxConcurrentCalls(1)
            .PrefetchCount(0)
            .ProcessDelay(0));

        var provider = services.BuildServiceProvider();
        await using var broker = provider.GetRequiredService<IQueueBroker>() as IAsyncDisposable;
        var queueBroker = provider.GetRequiredService<IQueueBroker>();
        var brokerService = provider.GetRequiredService<IQueueBrokerService>();

        var message = new ServiceBusQueueTrackMessage("purge-me");
        await queueBroker.Enqueue(message);

        await WaitForAsync(async () =>
        {
            var items = await brokerService.GetMessagesAsync();
            return items.Any(i => i.MessageId == message.MessageId);
        });

        await brokerService.PurgeMessagesAsync();

        var stats = await brokerService.GetMessageStatsAsync(isArchived: null);
        stats.Total.ShouldBe(0);
    }

    [SkippableFact]
    public async Task Broker_MultipleMessageTypes_OnlyCorrectHandlerTriggered()
    {
        Skip.IfNot(await this.fixture.WaitForServiceBusEmulatorReadyAsync(), "Service Bus emulator did not become ready within timeout");

        ServiceBusQueueTestMessageHandler.Reset();
        ServiceBusQueueOtherMessageHandler.Reset();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new QueueingRegistrationStore());
        services.AddSingleton(new QueueBrokerControlState());

        var context = new QueueingBuilderContext(services);
        context.WithServiceBusBroker(o => o
            .ConnectionString(this.fixture.ServiceBusEmulatorConnectionString)
            .QueueNamePrefix("test-")
            .AutoCreateQueue(false)
            .MaxConcurrentCalls(1)
            .PrefetchCount(0)
            .ProcessDelay(0));

        var provider = services.BuildServiceProvider();
        await using var broker = provider.GetRequiredService<IQueueBroker>() as IAsyncDisposable;
        var queueBroker = provider.GetRequiredService<IQueueBroker>();
        var brokerService = provider.GetRequiredService<IQueueBrokerService>();

        await queueBroker.Subscribe<ServiceBusQueueTestMessage, ServiceBusQueueTestMessageHandler>();
        await queueBroker.Subscribe<ServiceBusQueueOtherMessage, ServiceBusQueueOtherMessageHandler>();
        await Task.Delay(2000);

        var messageA = new ServiceBusQueueTestMessage("type-a");
        var messageB = new ServiceBusQueueOtherMessage("type-b");
        await queueBroker.Enqueue(messageA);
        await queueBroker.Enqueue(messageB);

        var processed = await WaitForAsync(
            () => Task.FromResult(
                ServiceBusQueueTestMessageHandler.ProcessedIds.Contains(messageA.MessageId) &&
                ServiceBusQueueOtherMessageHandler.ProcessedIds.Contains(messageB.MessageId)),
            attempts: 240,
            delayMilliseconds: 250);

        processed.ShouldBeTrue();
        ServiceBusQueueTestMessageHandler.ProcessedIds.ShouldContain(messageA.MessageId);
        ServiceBusQueueTestMessageHandler.ProcessedIds.ShouldNotContain(messageB.MessageId);
        ServiceBusQueueOtherMessageHandler.ProcessedIds.ShouldContain(messageB.MessageId);
        ServiceBusQueueOtherMessageHandler.ProcessedIds.ShouldNotContain(messageA.MessageId);

        var summary = await brokerService.GetSummaryAsync();
        summary.Succeeded.ShouldBe(2);
    }

    private static async Task<bool> WaitForAsync(Func<Task<bool>> condition, int attempts = 80, int delayMilliseconds = 250)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (await condition())
            {
                return true;
            }

            await Task.Delay(delayMilliseconds);
        }

        return false;
    }
}

public sealed class ServiceBusQueueTestMessage(string value) : QueueMessageBase
{
    public string Value { get; } = value;
}

public sealed class ServiceBusQueueTestMessageHandler : IQueueMessageHandler<ServiceBusQueueTestMessage>
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

    public Task Handle(ServiceBusQueueTestMessage message, CancellationToken cancellationToken)
    {
        Processed = true;
        LastMessageId = message.MessageId;
        ProcessedIdsList.Add(message.MessageId);
        return Task.CompletedTask;
    }
}

public sealed class ServiceBusQueueBeforeSubMessage(string value) : QueueMessageBase
{
    public string Value { get; } = value;
}

public sealed class ServiceBusQueueBeforeSubMessageHandler : IQueueMessageHandler<ServiceBusQueueBeforeSubMessage>
{
    public static bool Processed { get; private set; }

    public static string LastProcessedMessageId { get; private set; }

    public static void Reset()
    {
        Processed = false;
        LastProcessedMessageId = null;
    }

    public Task Handle(ServiceBusQueueBeforeSubMessage message, CancellationToken cancellationToken)
    {
        Processed = true;
        LastProcessedMessageId = message.MessageId;
        return Task.CompletedTask;
    }
}

public sealed class ServiceBusQueuePauseMessage(string value) : QueueMessageBase
{
    public string Value { get; } = value;
}

public sealed class ServiceBusQueuePauseMessageHandler : IQueueMessageHandler<ServiceBusQueuePauseMessage>
{
    public static bool Processed { get; private set; }

    public static void Reset()
    {
        Processed = false;
    }

    public Task Handle(ServiceBusQueuePauseMessage message, CancellationToken cancellationToken)
    {
        Processed = true;
        return Task.CompletedTask;
    }
}

public sealed class ServiceBusQueueFailMessage(string value) : QueueMessageBase
{
    public string Value { get; } = value;
}

public sealed class ServiceBusQueueFailMessageHandler : IQueueMessageHandler<ServiceBusQueueFailMessage>
{
    public static int AttemptCount { get; private set; }

    public static void Reset()
    {
        AttemptCount = 0;
    }

    public Task Handle(ServiceBusQueueFailMessage message, CancellationToken cancellationToken)
    {
        AttemptCount++;
        throw new InvalidOperationException($"Simulated failure for message {message.MessageId}");
    }
}

public sealed class ServiceBusQueueTrackMessage(string value) : QueueMessageBase
{
    public string Value { get; } = value;
}

public sealed class ServiceBusQueueOtherMessage(string value) : QueueMessageBase
{
    public string Value { get; } = value;
}

public sealed class ServiceBusQueueOtherMessageHandler : IQueueMessageHandler<ServiceBusQueueOtherMessage>
{
    private static readonly List<string> ProcessedIdsList = [];

    public static IReadOnlyList<string> ProcessedIds => ProcessedIdsList.AsReadOnly();

    public static void Reset()
    {
        ProcessedIdsList.Clear();
    }

    public Task Handle(ServiceBusQueueOtherMessage message, CancellationToken cancellationToken)
    {
        ProcessedIdsList.Add(message.MessageId);
        return Task.CompletedTask;
    }
}
