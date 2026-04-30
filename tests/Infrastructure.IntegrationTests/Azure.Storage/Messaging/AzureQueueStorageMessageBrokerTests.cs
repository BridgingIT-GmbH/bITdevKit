// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.Azure.Storage.Messaging;

using Application.IntegrationTests.Queueing;
using Application.Messaging;
using Infrastructure.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[Collection(nameof(TestEnvironmentCollection))]
[IntegrationTest("Infrastructure")]
public class AzureQueueStorageMessageBrokerTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
{
    private readonly TestEnvironmentFixture fixture = fixture.WithOutput(output);

    [Fact]
    public async Task Publish_SingleMessage_MessageHandledBySingleHandler()
    {
        AzureQueueStorageMessagingTestMessageHandler.Reset();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient<AzureQueueStorageMessagingTestMessageHandler>();

        var queueSuffix = $"-{Guid.NewGuid():N}";
        services.AddSingleton<IMessageBroker>(sp =>
        {
            var broker = new AzureQueueStorageMessageBroker(o => o
                .LoggerFactory(sp.GetRequiredService<ILoggerFactory>())
                .HandlerFactory(new ServiceProviderMessageHandlerFactory(sp))
                .Serializer(new SystemTextJsonSerializer())
                .ConnectionString(this.fixture.AzuriteConnectionString)
                .QueueNameSuffix(queueSuffix)
                .VisibilityTimeout(TimeSpan.FromSeconds(5))
                .PollingInterval(TimeSpan.FromMilliseconds(100))
                .ProcessDelay(0));

            return broker;
        });

        await using var provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<IMessageBroker>();

        await broker.Subscribe<AzureQueueStorageMessagingTestMessage, AzureQueueStorageMessagingTestMessageHandler>();
        await Task.Delay(500); // give poller time to start

        var message = new AzureQueueStorageMessagingTestMessage("hello", DateTime.UtcNow.Ticks);
        await broker.Publish(message, CancellationToken.None);

        var handled = await QueueingBrokerTestSupport.WaitForAsync(
            () => Task.FromResult(AzureQueueStorageMessagingTestMessageHandler.HandledMessageIds.Contains(message.MessageId)),
            attempts: 240,
            delayMilliseconds: 50);

        handled.ShouldBeTrue();
        AzureQueueStorageMessagingTestMessageHandler.HandledMessageIds.Count(i => i == message.MessageId).ShouldBe(1);
    }

    [Fact]
    public async Task Publish_SingleMessage_HandledByMultipleHandlers()
    {
        AzureQueueStorageMessagingTestMessageHandler.Reset();
        AzureQueueStorageMessagingOtherTestMessageHandler.Reset();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient<AzureQueueStorageMessagingTestMessageHandler>();
        services.AddTransient<AzureQueueStorageMessagingOtherTestMessageHandler>();

        var queueSuffix = $"-{Guid.NewGuid():N}";
        services.AddSingleton<IMessageBroker>(sp =>
        {
            var broker = new AzureQueueStorageMessageBroker(o => o
                .LoggerFactory(sp.GetRequiredService<ILoggerFactory>())
                .HandlerFactory(new ServiceProviderMessageHandlerFactory(sp))
                .Serializer(new SystemTextJsonSerializer())
                .ConnectionString(this.fixture.AzuriteConnectionString)
                .QueueNameSuffix(queueSuffix)
                .VisibilityTimeout(TimeSpan.FromSeconds(5))
                .PollingInterval(TimeSpan.FromMilliseconds(100))
                .ProcessDelay(0));

            return broker;
        });

        await using var provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<IMessageBroker>();

        await broker.Subscribe<AzureQueueStorageMessagingTestMessage, AzureQueueStorageMessagingTestMessageHandler>();
        await broker.Subscribe<AzureQueueStorageMessagingTestMessage, AzureQueueStorageMessagingOtherTestMessageHandler>();
        await Task.Delay(500);

        var message = new AzureQueueStorageMessagingTestMessage("fanout", DateTime.UtcNow.Ticks);
        await broker.Publish(message, CancellationToken.None);

        var handled = await QueueingBrokerTestSupport.WaitForAsync(
            () => Task.FromResult(
                AzureQueueStorageMessagingTestMessageHandler.HandledMessageIds.Contains(message.MessageId) &&
                AzureQueueStorageMessagingOtherTestMessageHandler.HandledMessageIds.Contains(message.MessageId)),
            attempts: 240,
            delayMilliseconds: 50);

        handled.ShouldBeTrue();
        AzureQueueStorageMessagingTestMessageHandler.HandledMessageIds.Count(i => i == message.MessageId).ShouldBe(1);
        AzureQueueStorageMessagingOtherTestMessageHandler.HandledMessageIds.Count(i => i == message.MessageId).ShouldBe(1);
    }

    [Fact]
    public async Task Publish_DifferentMessageTypes_OnlyRelevantHandlersTriggered()
    {
        AzureQueueStorageMessagingTestMessageHandler.Reset();
        AzureQueueStorageMessagingOtherMessageHandler.Reset();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient<AzureQueueStorageMessagingTestMessageHandler>();
        services.AddTransient<AzureQueueStorageMessagingOtherMessageHandler>();

        var queueSuffix = $"-{Guid.NewGuid():N}";
        services.AddSingleton<IMessageBroker>(sp =>
        {
            var broker = new AzureQueueStorageMessageBroker(o => o
                .LoggerFactory(sp.GetRequiredService<ILoggerFactory>())
                .HandlerFactory(new ServiceProviderMessageHandlerFactory(sp))
                .Serializer(new SystemTextJsonSerializer())
                .ConnectionString(this.fixture.AzuriteConnectionString)
                .QueueNameSuffix(queueSuffix)
                .VisibilityTimeout(TimeSpan.FromSeconds(5))
                .PollingInterval(TimeSpan.FromMilliseconds(100))
                .ProcessDelay(0));

            return broker;
        });

        await using var provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<IMessageBroker>();

        await broker.Subscribe<AzureQueueStorageMessagingTestMessage, AzureQueueStorageMessagingTestMessageHandler>();
        await broker.Subscribe<AzureQueueStorageMessagingOtherMessage, AzureQueueStorageMessagingOtherMessageHandler>();
        await Task.Delay(500);

        var messageA = new AzureQueueStorageMessagingTestMessage("type-a", DateTime.UtcNow.Ticks);
        var messageB = new AzureQueueStorageMessagingOtherMessage("type-b", DateTime.UtcNow.Ticks);

        await broker.Publish(messageA, CancellationToken.None);
        await broker.Publish(messageB, CancellationToken.None);

        var handled = await QueueingBrokerTestSupport.WaitForAsync(
            () => Task.FromResult(
                AzureQueueStorageMessagingTestMessageHandler.HandledMessageIds.Contains(messageA.MessageId) &&
                AzureQueueStorageMessagingOtherMessageHandler.HandledMessageIds.Contains(messageB.MessageId)),
            attempts: 240,
            delayMilliseconds: 50);

        handled.ShouldBeTrue();
        AzureQueueStorageMessagingTestMessageHandler.HandledMessageIds.ShouldContain(messageA.MessageId);
        AzureQueueStorageMessagingTestMessageHandler.HandledMessageIds.ShouldNotContain(messageB.MessageId);
        AzureQueueStorageMessagingOtherMessageHandler.HandledMessageIds.ShouldContain(messageB.MessageId);
        AzureQueueStorageMessagingOtherMessageHandler.HandledMessageIds.ShouldNotContain(messageA.MessageId);
    }

    [Fact]
    public async Task Publish_SingleMessageNoSubscriber_MessageHandledByNone()
    {
        AzureQueueStorageMessagingTestMessageHandler.Reset();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient<AzureQueueStorageMessagingTestMessageHandler>();

        var queueSuffix = $"-{Guid.NewGuid():N}";
        services.AddSingleton<IMessageBroker>(sp =>
        {
            var broker = new AzureQueueStorageMessageBroker(o => o
                .LoggerFactory(sp.GetRequiredService<ILoggerFactory>())
                .HandlerFactory(new ServiceProviderMessageHandlerFactory(sp))
                .Serializer(new SystemTextJsonSerializer())
                .ConnectionString(this.fixture.AzuriteConnectionString)
                .QueueNameSuffix(queueSuffix)
                .VisibilityTimeout(TimeSpan.FromSeconds(5))
                .PollingInterval(TimeSpan.FromMilliseconds(100))
                .ProcessDelay(0));

            return broker;
        });

        await using var provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<IMessageBroker>();

        // Do NOT subscribe before publishing
        var message = new AzureQueueStorageMessagingTestMessage("no-sub", DateTime.UtcNow.Ticks);
        await broker.Publish(message, CancellationToken.None);

        await Task.Delay(1000);

        AzureQueueStorageMessagingTestMessageHandler.HandledMessageIds.Count(i => i == message.MessageId).ShouldBe(0);
    }

    [Fact]
    public async Task Publish_MultipleMessages_AllHandled()
    {
        AzureQueueStorageMessagingTestMessageHandler.Reset();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient<AzureQueueStorageMessagingTestMessageHandler>();

        var queueSuffix = $"-{Guid.NewGuid():N}";
        services.AddSingleton<IMessageBroker>(sp =>
        {
            var broker = new AzureQueueStorageMessageBroker(o => o
                .LoggerFactory(sp.GetRequiredService<ILoggerFactory>())
                .HandlerFactory(new ServiceProviderMessageHandlerFactory(sp))
                .Serializer(new SystemTextJsonSerializer())
                .ConnectionString(this.fixture.AzuriteConnectionString)
                .QueueNameSuffix(queueSuffix)
                .VisibilityTimeout(TimeSpan.FromSeconds(5))
                .PollingInterval(TimeSpan.FromMilliseconds(100))
                .ProcessDelay(0));

            return broker;
        });

        await using var provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<IMessageBroker>();

        await broker.Subscribe<AzureQueueStorageMessagingTestMessage, AzureQueueStorageMessagingTestMessageHandler>();
        await Task.Delay(500);

        var messages = Enumerable.Range(0, 5)
            .Select(i => new AzureQueueStorageMessagingTestMessage($"msg-{i}", DateTime.UtcNow.Ticks))
            .ToList();

        foreach (var message in messages)
        {
            await broker.Publish(message, CancellationToken.None);
        }

        var allHandled = await QueueingBrokerTestSupport.WaitForAsync(
            () => Task.FromResult(messages.All(m => AzureQueueStorageMessagingTestMessageHandler.HandledMessageIds.Contains(m.MessageId))),
            attempts: 240,
            delayMilliseconds: 50);

        allHandled.ShouldBeTrue();
        messages.ForEach(m =>
            AzureQueueStorageMessagingTestMessageHandler.HandledMessageIds.Count(i => i == m.MessageId).ShouldBe(1));
    }
}

public sealed class AzureQueueStorageMessagingTestMessage(string value, long ticks) : MessageBase
{
    public string Value { get; } = value;

    public long Ticks { get; } = ticks;
}

public sealed class AzureQueueStorageMessagingTestMessageHandler : IMessageHandler<AzureQueueStorageMessagingTestMessage>
{
    private static readonly List<string> HandledMessageIdsList = [];

    public static IReadOnlyList<string> HandledMessageIds => HandledMessageIdsList.AsReadOnly();

    public static void Reset()
    {
        HandledMessageIdsList.Clear();
    }

    public Task Handle(AzureQueueStorageMessagingTestMessage message, CancellationToken cancellationToken)
    {
        HandledMessageIdsList.Add(message.MessageId);
        return Task.CompletedTask;
    }
}

public sealed class AzureQueueStorageMessagingOtherTestMessageHandler : IMessageHandler<AzureQueueStorageMessagingTestMessage>
{
    private static readonly List<string> HandledMessageIdsList = [];

    public static IReadOnlyList<string> HandledMessageIds => HandledMessageIdsList.AsReadOnly();

    public static void Reset()
    {
        HandledMessageIdsList.Clear();
    }

    public Task Handle(AzureQueueStorageMessagingTestMessage message, CancellationToken cancellationToken)
    {
        HandledMessageIdsList.Add(message.MessageId);
        return Task.CompletedTask;
    }
}

public sealed class AzureQueueStorageMessagingOtherMessage(string value, long ticks) : MessageBase
{
    public string Value { get; } = value;

    public long Ticks { get; } = ticks;
}

public sealed class AzureQueueStorageMessagingOtherMessageHandler : IMessageHandler<AzureQueueStorageMessagingOtherMessage>
{
    private static readonly List<string> HandledMessageIdsList = [];

    public static IReadOnlyList<string> HandledMessageIds => HandledMessageIdsList.AsReadOnly();

    public static void Reset()
    {
        HandledMessageIdsList.Clear();
    }

    public Task Handle(AzureQueueStorageMessagingOtherMessage message, CancellationToken cancellationToken)
    {
        HandledMessageIdsList.Add(message.MessageId);
        return Task.CompletedTask;
    }
}
