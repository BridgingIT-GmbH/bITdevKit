// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.RabbitMQ.Messaging;

using Application.IntegrationTests.Queueing;
using Application.Messaging;
using Infrastructure.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[Collection(nameof(TestEnvironmentCollection))]
public class RabbitMQMessageBrokerTests
{
    private readonly TestEnvironmentFixture fixture;

    public RabbitMQMessageBrokerTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
    {
        this.fixture = fixture.WithOutput(output);
    }

    [Fact]
    public async Task Publish_SingleMessage_MessageHandledBySingleHandler()
    {
        RabbitMQTestMessageHandler.Reset();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient<RabbitMQTestMessageHandler>();

        var exchangeName = $"test-exchange-{Guid.NewGuid():N}";
        services.AddSingleton<IMessageBroker>(sp =>
        {
            var broker = new RabbitMQMessageBroker(o => o
                .LoggerFactory(sp.GetRequiredService<ILoggerFactory>())
                .HandlerFactory(new ServiceProviderMessageHandlerFactory(sp))
                .Serializer(new SystemTextJsonSerializer())
                .ConnectionString(this.fixture.RabbitMQConnectionString)
                .ExchangeName(exchangeName)
                .ProcessDelay(0)
                .DurableEnabled(false)
                .ExclusiveQueueEnabled(false)
                .AutoDeleteQueueEnabled(true));

            return broker;
        });

        using var provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<IMessageBroker>();

        await broker.Subscribe<RabbitMQTestMessage, RabbitMQTestMessageHandler>();

        var message = new RabbitMQTestMessage("hello", DateTime.UtcNow.Ticks);
        await broker.Publish(message, CancellationToken.None);

        var handled = await QueueingBrokerTestSupport.WaitForAsync(
            () => Task.FromResult(RabbitMQTestMessageHandler.HandledMessageIds.Contains(message.MessageId)),
            attempts: 120,
            delayMilliseconds: 50);

        handled.ShouldBeTrue();
        RabbitMQTestMessageHandler.HandledMessageIds.Count(i => i == message.MessageId).ShouldBe(1);
    }

    [Fact]
    public async Task Publish_DifferentExchanges_MessagesIsolated()
    {
        RabbitMQTestMessageHandler.Reset();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient<RabbitMQTestMessageHandler>();

        var exchangeNameA = $"test-exchange-a-{Guid.NewGuid():N}";
        var exchangeNameB = $"test-exchange-b-{Guid.NewGuid():N}";

        services.AddSingleton<IMessageBroker>(sp =>
        {
            var brokerA = new RabbitMQMessageBroker(o => o
                .LoggerFactory(sp.GetRequiredService<ILoggerFactory>())
                .HandlerFactory(new ServiceProviderMessageHandlerFactory(sp))
                .Serializer(new SystemTextJsonSerializer())
                .ConnectionString(this.fixture.RabbitMQConnectionString)
                .ExchangeName(exchangeNameA)
                .ProcessDelay(0)
                .DurableEnabled(false)
                .ExclusiveQueueEnabled(false)
                .AutoDeleteQueueEnabled(true));

            return brokerA;
        });

        using var provider = services.BuildServiceProvider();
        var brokerA = provider.GetRequiredService<IMessageBroker>();

        var brokerB = new RabbitMQMessageBroker(o => o
            .LoggerFactory(provider.GetRequiredService<ILoggerFactory>())
            .HandlerFactory(new ServiceProviderMessageHandlerFactory(provider))
            .Serializer(new SystemTextJsonSerializer())
            .ConnectionString(this.fixture.RabbitMQConnectionString)
            .ExchangeName(exchangeNameB)
            .ProcessDelay(0)
            .DurableEnabled(false)
            .ExclusiveQueueEnabled(false)
            .AutoDeleteQueueEnabled(true));

        await brokerA.Subscribe<RabbitMQTestMessage, RabbitMQTestMessageHandler>();
        await brokerB.Subscribe<RabbitMQTestMessage, RabbitMQTestMessageHandler>();

        var message = new RabbitMQTestMessage("isolated", DateTime.UtcNow.Ticks);
        await brokerA.Publish(message, CancellationToken.None);

        var handled = await QueueingBrokerTestSupport.WaitForAsync(
            () => Task.FromResult(RabbitMQTestMessageHandler.HandledMessageIds.Contains(message.MessageId)),
            attempts: 120,
            delayMilliseconds: 50);

        handled.ShouldBeTrue();
        RabbitMQTestMessageHandler.HandledMessageIds.Count(i => i == message.MessageId).ShouldBe(1);
    }

    [Fact]
    public async Task Publish_SingleMessageNoSubscriber_MessageHandledByNone()
    {
        RabbitMQTestMessageHandler.Reset();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient<RabbitMQTestMessageHandler>();

        var exchangeName = $"test-exchange-{Guid.NewGuid():N}";
        services.AddSingleton<IMessageBroker>(sp =>
        {
            var broker = new RabbitMQMessageBroker(o => o
                .LoggerFactory(sp.GetRequiredService<ILoggerFactory>())
                .HandlerFactory(new ServiceProviderMessageHandlerFactory(sp))
                .Serializer(new SystemTextJsonSerializer())
                .ConnectionString(this.fixture.RabbitMQConnectionString)
                .ExchangeName(exchangeName)
                .ProcessDelay(0)
                .DurableEnabled(false)
                .ExclusiveQueueEnabled(false)
                .AutoDeleteQueueEnabled(true));

            return broker;
        });

        using var provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<IMessageBroker>();

        // Do NOT subscribe before publishing
        var message = new RabbitMQTestMessage("no-sub", DateTime.UtcNow.Ticks);
        await broker.Publish(message, CancellationToken.None);

        await Task.Delay(1000);

        RabbitMQTestMessageHandler.HandledMessageIds.Count(i => i == message.MessageId).ShouldBe(0);
    }

    [Fact]
    public async Task Publish_MultipleMessages_AllHandled()
    {
        RabbitMQTestMessageHandler.Reset();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient<RabbitMQTestMessageHandler>();

        var exchangeName = $"test-exchange-{Guid.NewGuid():N}";
        services.AddSingleton<IMessageBroker>(sp =>
        {
            var broker = new RabbitMQMessageBroker(o => o
                .LoggerFactory(sp.GetRequiredService<ILoggerFactory>())
                .HandlerFactory(new ServiceProviderMessageHandlerFactory(sp))
                .Serializer(new SystemTextJsonSerializer())
                .ConnectionString(this.fixture.RabbitMQConnectionString)
                .ExchangeName(exchangeName)
                .ProcessDelay(0)
                .DurableEnabled(false)
                .ExclusiveQueueEnabled(false)
                .AutoDeleteQueueEnabled(true));

            return broker;
        });

        using var provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<IMessageBroker>();

        await broker.Subscribe<RabbitMQTestMessage, RabbitMQTestMessageHandler>();

        var messages = Enumerable.Range(0, 5)
            .Select(i => new RabbitMQTestMessage($"msg-{i}", DateTime.UtcNow.Ticks))
            .ToList();

        foreach (var message in messages)
        {
            await broker.Publish(message, CancellationToken.None);
        }

        var allHandled = await QueueingBrokerTestSupport.WaitForAsync(
            () => Task.FromResult(messages.All(m => RabbitMQTestMessageHandler.HandledMessageIds.Contains(m.MessageId))),
            attempts: 200,
            delayMilliseconds: 50);

        allHandled.ShouldBeTrue();
        messages.ForEach(m =>
            RabbitMQTestMessageHandler.HandledMessageIds.Count(i => i == m.MessageId).ShouldBe(1));
    }

    [Fact]
    public async Task Publish_MultipleMessageTypes_OnlyCorrectHandlerTriggered()
    {
        RabbitMQTestMessageHandler.Reset();
        RabbitMQOtherTestMessageHandler.Reset();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient<RabbitMQTestMessageHandler>();
        services.AddTransient<RabbitMQOtherTestMessageHandler>();

        var exchangeName = $"test-exchange-{Guid.NewGuid():N}";
        services.AddSingleton<IMessageBroker>(sp =>
        {
            var broker = new RabbitMQMessageBroker(o => o
                .LoggerFactory(sp.GetRequiredService<ILoggerFactory>())
                .HandlerFactory(new ServiceProviderMessageHandlerFactory(sp))
                .Serializer(new SystemTextJsonSerializer())
                .ConnectionString(this.fixture.RabbitMQConnectionString)
                .ExchangeName(exchangeName)
                .ProcessDelay(0)
                .DurableEnabled(false)
                .ExclusiveQueueEnabled(false)
                .AutoDeleteQueueEnabled(true));

            return broker;
        });

        using var provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<IMessageBroker>();

        await broker.Subscribe<RabbitMQTestMessage, RabbitMQTestMessageHandler>();
        await broker.Subscribe<RabbitMQOtherTestMessage, RabbitMQOtherTestMessageHandler>();

        var messageA = new RabbitMQTestMessage("type-a", DateTime.UtcNow.Ticks);
        var messageB = new RabbitMQOtherTestMessage("type-b", DateTime.UtcNow.Ticks);
        await broker.Publish(messageA, CancellationToken.None);
        await broker.Publish(messageB, CancellationToken.None);

        var handled = await QueueingBrokerTestSupport.WaitForAsync(
            () => Task.FromResult(
                RabbitMQTestMessageHandler.HandledMessageIds.Contains(messageA.MessageId) &&
                RabbitMQOtherTestMessageHandler.HandledMessageIds.Contains(messageB.MessageId)),
            attempts: 200,
            delayMilliseconds: 50);

        handled.ShouldBeTrue();
        RabbitMQTestMessageHandler.HandledMessageIds.ShouldContain(messageA.MessageId);
        RabbitMQTestMessageHandler.HandledMessageIds.ShouldNotContain(messageB.MessageId);
        RabbitMQOtherTestMessageHandler.HandledMessageIds.ShouldContain(messageB.MessageId);
        RabbitMQOtherTestMessageHandler.HandledMessageIds.ShouldNotContain(messageA.MessageId);
    }
}

public sealed class RabbitMQTestMessage(string value, long ticks) : MessageBase
{
    public string Value { get; } = value;

    public long Ticks { get; } = ticks;
}

public sealed class RabbitMQTestMessageHandler(ILoggerFactory loggerFactory) : MessageHandlerBase<RabbitMQTestMessage>(loggerFactory)
{
    private static readonly List<string> HandledIds = [];

    public static IReadOnlyList<string> HandledMessageIds => HandledIds.AsReadOnly();

    public static void Reset()
    {
        HandledIds.Clear();
    }

    public override async Task Handle(RabbitMQTestMessage message, CancellationToken cancellationToken)
    {
        lock (HandledIds)
        {
            HandledIds.Add(message.MessageId);
        }

        this.Logger.LogInformation(
            "{LogKey} handled message (name={MessageName}, id={MessageId})",
            Application.Messaging.Constants.LogKey,
            message.GetType().PrettyName(),
            message.MessageId);

        await Task.CompletedTask;
    }
}

public sealed class RabbitMQOtherTestMessage(string value, long ticks) : MessageBase
{
    public string Value { get; } = value;

    public long Ticks { get; } = ticks;
}

public sealed class RabbitMQOtherTestMessageHandler(ILoggerFactory loggerFactory) : MessageHandlerBase<RabbitMQOtherTestMessage>(loggerFactory)
{
    private static readonly List<string> HandledIds = [];

    public static IReadOnlyList<string> HandledMessageIds => HandledIds.AsReadOnly();

    public static void Reset()
    {
        HandledIds.Clear();
    }

    public override async Task Handle(RabbitMQOtherTestMessage message, CancellationToken cancellationToken)
    {
        lock (HandledIds)
        {
            HandledIds.Add(message.MessageId);
        }

        this.Logger.LogInformation(
            "{LogKey} other handler handled message (name={MessageName}, id={MessageId})",
            Application.Messaging.Constants.LogKey,
            message.GetType().PrettyName(),
            message.MessageId);

        await Task.CompletedTask;
    }
}
