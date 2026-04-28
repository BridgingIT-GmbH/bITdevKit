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
        (sut as IAsyncDisposable)?.DisposeAsync().AsTask().Wait();
        await Task.Delay(2000);

        // Scenario 2: No subscriber
        MessageState.Reset();
        sut = this.CreateMessageBroker();

        var message2 = new MessageStub { FirstName = "Jane", LastName = "Doe" };
        await sut.Publish(message2);
        await Task.Delay(2000);

        MessageState.HandledMessageIds.ShouldNotContain(message2.MessageId);

        (sut as IAsyncDisposable)?.DisposeAsync().AsTask().Wait();
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
        (sut as IAsyncDisposable)?.DisposeAsync().AsTask().Wait();
    }

    private IMessageBroker CreateMessageBroker()
    {
        return new ServiceBusMessageBroker(o => o
            .ConnectionString(this.fixture.ServiceBusEmulatorConnectionString)
            .TopicScope("test")
            .AutoCreateTopic(false)
            .HandlerFactory(new ServiceProviderMessageHandlerFactory(this.fixture.ServiceProvider))
            .Serializer(new SystemTextJsonSerializer()));
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
