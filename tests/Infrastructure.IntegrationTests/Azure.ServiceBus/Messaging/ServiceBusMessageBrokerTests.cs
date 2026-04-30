// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.Azure;

using Application.Messaging;
using FluentValidation.Results;
using Infrastructure.Azure;
using Microsoft.Extensions.DependencyInjection;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))]
public class ServiceBusMessageBrokerTests
{
    private static readonly MessageState MessageState = new();
    private readonly TestEnvironmentFixture fixture;

    public ServiceBusMessageBrokerTests(TestEnvironmentFixture fixture)
    {
        this.fixture = fixture;
        this.fixture.Services.AddSingleton(MessageState);
    }

    [SkippableFact]
    public async Task Publish_Messages_HandledCorrectly()
    {
        Skip.IfNot(await this.fixture.WaitForServiceBusEmulatorReadyAsync(), "Service Bus emulator did not become ready within timeout");

        // Scenario 1: Single handler
        MessageState.Reset();
        var sut = this.CreateMessageBroker();
        await sut.Subscribe<MessageStub, MessageStubHandler>();
        await Task.Delay(2000);

        var message1 = new MessageStub { FirstName = "John", LastName = "Doe" };
        await sut.Publish(message1);

        var processed1 = await WaitForAsync(() => Task.FromResult(MessageState.HandledMessageIds.Contains(message1.MessageId)), attempts: 240);
        processed1.ShouldBeTrue($"Message {message1.MessageId} was not handled within the timeout");

        await sut.Unsubscribe<MessageStub, MessageStubHandler>();
        await (sut as IAsyncDisposable).DisposeAsync().AsTask();
        await Task.Delay(2000);

        // Scenario 2: No subscriber
        MessageState.Reset();
        sut = this.CreateMessageBroker();

        var message2 = new MessageStub { FirstName = "Jane", LastName = "Doe" };
        await sut.Publish(message2);
        await Task.Delay(2000);

        MessageState.HandledMessageIds.ShouldNotContain(message2.MessageId);

        await (sut as IAsyncDisposable).DisposeAsync().AsTask();
        await Task.Delay(2000);

        // Scenario 3: Multiple handlers
        MessageState.Reset();
        sut = this.CreateMessageBroker();
        await sut.Subscribe<MessageStub, MessageStubHandler>();
        await sut.Subscribe<MessageStub, AnotherMessageStubHandler>();
        await Task.Delay(2000);

        var message3 = new MessageStub { FirstName = "John", LastName = "Doe" };
        await sut.Publish(message3);

        var processed3 = await WaitForAsync(() => Task.FromResult(MessageState.HandledMessageIds.Count(i => i == message3.MessageId) == 2), attempts: 240);
        processed3.ShouldBeTrue($"Message {message3.MessageId} was not handled by both handlers within the timeout");

        await sut.Unsubscribe<MessageStub, MessageStubHandler>();
        await sut.Unsubscribe<MessageStub, AnotherMessageStubHandler>();
        await (sut as IAsyncDisposable).DisposeAsync().AsTask();
    }

    [SkippableFact]
    public async Task Publish_SingleMessage_MessageHandledBySingleHandler()
    {
        Skip.IfNot(await this.fixture.WaitForServiceBusEmulatorReadyAsync(), "Service Bus emulator did not become ready within timeout");

        ServiceBusTestMessageHandler.Reset();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient<ServiceBusTestMessageHandler>();

        using var provider = services.BuildServiceProvider();
        var broker = this.CreateMessageBroker(provider);

        await broker.Subscribe<ServiceBusTestMessage, ServiceBusTestMessageHandler>();
        await Task.Delay(2000);

        var message = new ServiceBusTestMessage("hello", DateTime.UtcNow.Ticks);
        await broker.Publish(message, CancellationToken.None);

        var handled = await WaitForAsync(
            () => Task.FromResult(ServiceBusTestMessageHandler.HandledMessageIds.Contains(message.MessageId)),
            attempts: 240,
            delayMilliseconds: 250);

        await (broker as IAsyncDisposable).DisposeAsync().AsTask();

        handled.ShouldBeTrue();
        ServiceBusTestMessageHandler.HandledMessageIds.Count(i => i == message.MessageId).ShouldBe(1);
    }

    [SkippableFact]
    public async Task Publish_SingleMessageNoSubscriber_MessageHandledByNone()
    {
        Skip.IfNot(await this.fixture.WaitForServiceBusEmulatorReadyAsync(), "Service Bus emulator did not become ready within timeout");

        ServiceBusTestMessageHandler.Reset();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient<ServiceBusTestMessageHandler>();

        using var provider = services.BuildServiceProvider();
        var broker = this.CreateMessageBroker(provider);

        // Do NOT subscribe before publishing
        var message = new ServiceBusTestMessage("no-sub", DateTime.UtcNow.Ticks);
        await broker.Publish(message, CancellationToken.None);

        await Task.Delay(2000);

        await (broker as IAsyncDisposable).DisposeAsync().AsTask();

        ServiceBusTestMessageHandler.HandledMessageIds.Count(i => i == message.MessageId).ShouldBe(0);
    }

    [SkippableFact]
    public async Task Publish_MultipleMessages_AllHandled()
    {
        Skip.IfNot(await this.fixture.WaitForServiceBusEmulatorReadyAsync(), "Service Bus emulator did not become ready within timeout");

        ServiceBusTestMessageHandler.Reset();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient<ServiceBusTestMessageHandler>();

        using var provider = services.BuildServiceProvider();
        var broker = this.CreateMessageBroker(provider);

        await broker.Subscribe<ServiceBusTestMessage, ServiceBusTestMessageHandler>();
        await Task.Delay(2000);

        var messages = Enumerable.Range(0, 5)
            .Select(i => new ServiceBusTestMessage($"msg-{i}", DateTime.UtcNow.Ticks))
            .ToList();

        foreach (var message in messages)
        {
            await broker.Publish(message, CancellationToken.None);
        }

        var allHandled = await WaitForAsync(
            () => Task.FromResult(messages.All(m => ServiceBusTestMessageHandler.HandledMessageIds.Contains(m.MessageId))),
            attempts: 240,
            delayMilliseconds: 250);

        await (broker as IAsyncDisposable).DisposeAsync().AsTask();

        allHandled.ShouldBeTrue();
        messages.ForEach(m =>
            ServiceBusTestMessageHandler.HandledMessageIds.Count(i => i == m.MessageId).ShouldBe(1));
    }

    [SkippableFact]
    public async Task Publish_MultipleMessageTypes_OnlyCorrectHandlerTriggered()
    {
        Skip.IfNot(await this.fixture.WaitForServiceBusEmulatorReadyAsync(), "Service Bus emulator did not become ready within timeout");

        ServiceBusTestMessageHandler.Reset();
        ServiceBusOtherTestMessageHandler.Reset();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient<ServiceBusTestMessageHandler>();
        services.AddTransient<ServiceBusOtherTestMessageHandler>();

        using var provider = services.BuildServiceProvider();
        var broker = this.CreateMessageBroker(provider);

        await broker.Subscribe<ServiceBusTestMessage, ServiceBusTestMessageHandler>();
        await broker.Subscribe<ServiceBusOtherTestMessage, ServiceBusOtherTestMessageHandler>();
        await Task.Delay(2000);

        var messageA = new ServiceBusTestMessage("type-a", DateTime.UtcNow.Ticks);
        var messageB = new ServiceBusOtherTestMessage("type-b", DateTime.UtcNow.Ticks);
        await broker.Publish(messageA, CancellationToken.None);
        await broker.Publish(messageB, CancellationToken.None);

        var handled = await WaitForAsync(
            () => Task.FromResult(
                ServiceBusTestMessageHandler.HandledMessageIds.Contains(messageA.MessageId) &&
                ServiceBusOtherTestMessageHandler.HandledMessageIds.Contains(messageB.MessageId)),
            attempts: 240,
            delayMilliseconds: 250);

        handled.ShouldBeTrue();
        ServiceBusTestMessageHandler.HandledMessageIds.ShouldContain(messageA.MessageId);
        ServiceBusTestMessageHandler.HandledMessageIds.ShouldNotContain(messageB.MessageId);
        ServiceBusOtherTestMessageHandler.HandledMessageIds.ShouldContain(messageB.MessageId);
        ServiceBusOtherTestMessageHandler.HandledMessageIds.ShouldNotContain(messageA.MessageId);

        await (broker as IAsyncDisposable).DisposeAsync().AsTask();
    }

    private IMessageBroker CreateMessageBroker(IServiceProvider serviceProvider = null)
    {
        serviceProvider ??= this.fixture.ServiceProvider;

        return new ServiceBusMessageBroker(o => o
            .ConnectionString(this.fixture.ServiceBusEmulatorConnectionString)
            .TopicScope("test")
            .AutoCreateTopic(false)
            .HandlerFactory(new ServiceProviderMessageHandlerFactory(serviceProvider))
            .Serializer(new SystemTextJsonSerializer())
            .ProcessDelay(0));
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

public sealed class MessageState
{
    private readonly List<string> handledMessageIds = [];

    public IReadOnlyList<string> HandledMessageIds => this.handledMessageIds.AsReadOnly();

    public void Add(string messageId)
    {
        this.handledMessageIds.Add(messageId);
    }

    public void Reset()
    {
        this.handledMessageIds.Clear();
    }
}

public class MessageStub : IMessage
{
    public string MessageId { get; set; } = GuidGenerator.CreateSequential().ToString("N");

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public string FirstName { get; set; }

    public string LastName { get; set; }

    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

    public ValidationResult Validate()
    {
        return new ValidationResult();
    }
}

public class MessageStubHandler(MessageState state) : IMessageHandler<MessageStub>
{
    public Task Handle(MessageStub message, CancellationToken cancellationToken)
    {
        state.Add(message.MessageId);
        return Task.CompletedTask;
    }
}

public class AnotherMessageStubHandler(MessageState state) : IMessageHandler<MessageStub>
{
    public Task Handle(MessageStub message, CancellationToken cancellationToken)
    {
        state.Add(message.MessageId);
        return Task.CompletedTask;
    }
}

public sealed class ServiceBusTestMessage(string value, long ticks) : MessageBase
{
    public string Value { get; } = value;

    public long Ticks { get; } = ticks;
}

public sealed class ServiceBusTestMessageHandler : IMessageHandler<ServiceBusTestMessage>
{
    private static readonly List<string> HandledIds = [];

    public static IReadOnlyList<string> HandledMessageIds => HandledIds.AsReadOnly();

    public static void Reset()
    {
        HandledIds.Clear();
    }

    public Task Handle(ServiceBusTestMessage message, CancellationToken cancellationToken)
    {
        lock (HandledIds)
        {
            HandledIds.Add(message.MessageId);
        }

        return Task.CompletedTask;
    }
}

public sealed class ServiceBusOtherTestMessage(string value, long ticks) : MessageBase
{
    public string Value { get; } = value;

    public long Ticks { get; } = ticks;
}

public sealed class ServiceBusOtherTestMessageHandler : IMessageHandler<ServiceBusOtherTestMessage>
{
    private static readonly List<string> HandledIds = [];

    public static IReadOnlyList<string> HandledMessageIds => HandledIds.AsReadOnly();

    public static void Reset()
    {
        HandledIds.Clear();
    }

    public Task Handle(ServiceBusOtherTestMessage message, CancellationToken cancellationToken)
    {
        lock (HandledIds)
        {
            HandledIds.Add(message.MessageId);
        }

        return Task.CompletedTask;
    }
}
