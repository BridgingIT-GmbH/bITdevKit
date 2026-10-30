// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.Queueing;

using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Queueing;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

public class EntityFrameworkQueueBrokerTests
{
    [Fact]
    public async Task EnqueueAndWait_AutoSaveDisabled_PersistsMessageWithSubscriptionState()
    {
        // Arrange
        await using var context = CreateContext();
        var handlerFactory = Substitute.For<IQueueMessageHandlerFactory>();
        handlerFactory.Create(typeof(CapturingQueueMessageHandler)).Returns(QueueMessageHandlerFactoryResult.Create(new CapturingQueueMessageHandler()));
        var sut = CreateSut(context, handlerFactory, options => options.AutoSave = false);
        await sut.Subscribe<TestQueueMessage, CapturingQueueMessageHandler>();

        var message = new TestQueueMessage("value");

        // Act
        await sut.EnqueueAndWait(message);

        // Assert
        var persisted = await context.QueueMessages.SingleAsync();
        persisted.MessageId.ShouldBe(message.MessageId);
        persisted.QueueName.ShouldBe(CreateQueueName(typeof(TestQueueMessage)));
        persisted.Status.ShouldBe(QueueMessageStatus.Pending);
        persisted.ExpiresOn.ShouldNotBeNull();
    }

    [Fact]
    public async Task ProcessStoredMessageAsync_ExpiredMessage_MarksMessageAsExpired()
    {
        // Arrange
        var handlerFactory = Substitute.For<IQueueMessageHandlerFactory>();
        var sut = CreateSut(CreateContext(), handlerFactory);
        var storedMessage = CreateStoredMessage(expiresOn: DateTimeOffset.UtcNow.AddMinutes(-1));

        // Act
        await sut.ProcessStoredMessageAsync(storedMessage);

        // Assert
        storedMessage.Status.ShouldBe(QueueMessageStatus.Expired);
        storedMessage.LastError.ShouldBe("message expired before processing completed");
        storedMessage.ProcessedDate.ShouldNotBeNull();
    }

    [Fact]
    public async Task ProcessStoredMessageAsync_PausedQueue_ResetsMessageToPendingWithoutProcessing()
    {
        // Arrange
        var controlState = new QueueBrokerControlState();
        controlState.PauseQueue(CreateQueueName(typeof(TestQueueMessage)));

        var handlerFactory = Substitute.For<IQueueMessageHandlerFactory>();
        var sut = CreateSut(CreateContext(), handlerFactory, controlState: controlState);
        var storedMessage = CreateStoredMessage();

        // Act
        await sut.ProcessStoredMessageAsync(storedMessage);

        // Assert
        storedMessage.Status.ShouldBe(QueueMessageStatus.Pending);
        storedMessage.ProcessedDate.ShouldBeNull();
        storedMessage.AttemptCount.ShouldBe(0);
    }

    [Fact]
    public async Task ProcessStoredMessageAsync_UnresolvableType_DeadLettersMessage()
    {
        // Arrange
        var handlerFactory = Substitute.For<IQueueMessageHandlerFactory>();
        var sut = CreateSut(CreateContext(), handlerFactory, options => options.MaxDeliveryAttempts = 4);
        var storedMessage = CreateStoredMessage(type: "Missing.Type, Missing.Assembly");

        // Act
        await sut.ProcessStoredMessageAsync(storedMessage);

        // Assert
        storedMessage.Status.ShouldBe(QueueMessageStatus.DeadLettered);
        storedMessage.AttemptCount.ShouldBe(4);
        storedMessage.LastError.ShouldContain("message type could not be resolved");
        storedMessage.ProcessedDate.ShouldNotBeNull();
    }

    [Fact]
    public async Task ProcessStoredMessageAsync_NoSubscription_MarksMessageAsWaitingForHandler()
    {
        // Arrange
        var handlerFactory = Substitute.For<IQueueMessageHandlerFactory>();
        var sut = CreateSut(CreateContext(), handlerFactory);
        var storedMessage = CreateStoredMessage();

        // Act
        await sut.ProcessStoredMessageAsync(storedMessage);

        // Assert
        storedMessage.Status.ShouldBe(QueueMessageStatus.WaitingForHandler);
        storedMessage.LastError.ShouldBeNull();
        storedMessage.ProcessedDate.ShouldBeNull();
        storedMessage.AttemptCount.ShouldBe(1);
    }

    [Fact]
    public async Task ProcessStoredMessageAsync_WhenHandlerSucceeds_MarksMessageAsSucceeded()
    {
        // Arrange
        var handler = new CapturingQueueMessageHandler();
        var handlerFactory = Substitute.For<IQueueMessageHandlerFactory>();
        handlerFactory.Create(typeof(CapturingQueueMessageHandler)).Returns(QueueMessageHandlerFactoryResult.Create(handler));

        var sut = CreateSut(CreateContext(), handlerFactory);
        await sut.Subscribe<TestQueueMessage, CapturingQueueMessageHandler>();

        var storedMessage = CreateStoredMessage();
        storedMessage.Properties["correlationId"] = "abc-123";

        // Act
        await sut.ProcessStoredMessageAsync(storedMessage);

        // Assert
        storedMessage.Status.ShouldBe(QueueMessageStatus.Succeeded);
        storedMessage.LastError.ShouldBeNull();
        storedMessage.ProcessedDate.ShouldNotBeNull();
        storedMessage.RegisteredHandlerType.ShouldBe(typeof(CapturingQueueMessageHandler).FullName);
        handler.ReceivedMessage.ShouldNotBeNull();
        handler.ReceivedMessage.Properties["correlationId"].ToString().ShouldBe("abc-123");
    }

    [Fact]
    public async Task ProcessStoredMessageAsync_WhenHandlerFailsBeforeMaxAttempts_MarksMessageAsFailed()
    {
        // Arrange
        var handlerFactory = Substitute.For<IQueueMessageHandlerFactory>();
        handlerFactory.Create(typeof(ThrowingQueueMessageHandler)).Returns(QueueMessageHandlerFactoryResult.Create(new ThrowingQueueMessageHandler()));

        var sut = CreateSut(CreateContext(), handlerFactory, options => options.MaxDeliveryAttempts = 3);
        await sut.Subscribe<TestQueueMessage, ThrowingQueueMessageHandler>();

        var storedMessage = CreateStoredMessage();

        // Act
        await sut.ProcessStoredMessageAsync(storedMessage);

        // Assert
        storedMessage.Status.ShouldBe(QueueMessageStatus.Failed);
        storedMessage.AttemptCount.ShouldBe(1);
        storedMessage.LastError.ShouldBe("handler processing failed");
        storedMessage.ProcessedDate.ShouldBeNull();
    }

    [Fact]
    public async Task ProcessStoredMessageAsync_WhenHandlerFailsAtMaxAttempts_MarksMessageAsDeadLettered()
    {
        // Arrange
        var handlerFactory = Substitute.For<IQueueMessageHandlerFactory>();
        handlerFactory.Create(typeof(ThrowingQueueMessageHandler)).Returns(QueueMessageHandlerFactoryResult.Create(new ThrowingQueueMessageHandler()));

        var sut = CreateSut(CreateContext(), handlerFactory, options => options.MaxDeliveryAttempts = 2);
        await sut.Subscribe<TestQueueMessage, ThrowingQueueMessageHandler>();

        var storedMessage = CreateStoredMessage(attemptCount: 1);

        // Act
        await sut.ProcessStoredMessageAsync(storedMessage);

        // Assert
        storedMessage.Status.ShouldBe(QueueMessageStatus.DeadLettered);
        storedMessage.AttemptCount.ShouldBe(2);
        storedMessage.LastError.ShouldBe("max delivery attempts reached");
        storedMessage.ProcessedDate.ShouldNotBeNull();
    }

    [Fact]
    public async Task ProcessStoredMessageAsync_WhenPayloadCannotDeserialize_DeadLettersMessage()
    {
        // Arrange
        var serializer = Substitute.For<ISerializer>();
        serializer.Deserialize(Arg.Any<Stream>(), Arg.Any<Type>()).Returns("not a queue message");

        var handlerFactory = Substitute.For<IQueueMessageHandlerFactory>();
        handlerFactory.Create(typeof(CapturingQueueMessageHandler)).Returns(QueueMessageHandlerFactoryResult.Create(new CapturingQueueMessageHandler()));
        var sut = CreateSut(CreateContext(), handlerFactory, options => options.Serializer = serializer);
        await sut.Subscribe<TestQueueMessage, CapturingQueueMessageHandler>();

        var storedMessage = CreateStoredMessage(content: Convert.ToBase64String([1, 2, 3]));

        // Act
        await sut.ProcessStoredMessageAsync(storedMessage);

        // Assert
        storedMessage.Status.ShouldBe(QueueMessageStatus.DeadLettered);
        storedMessage.LastError.ShouldBe("message content could not be deserialized");
        storedMessage.ProcessedDate.ShouldNotBeNull();
    }

    private static TestQueueDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestQueueDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new TestQueueDbContext(options);
    }

    private static EntityFrameworkQueueBroker<TestQueueDbContext> CreateSut(
        TestQueueDbContext context,
        IQueueMessageHandlerFactory handlerFactory,
        Action<EntityFrameworkQueueBrokerOptions> configureOptions = null,
        QueueBrokerControlState controlState = null)
    {
        var options = new EntityFrameworkQueueBrokerOptions
        {
            HandlerFactory = handlerFactory,
            AutoSave = true,
            QueueNamePrefix = "q-",
            QueueNameSuffix = "-broker",
            MessageExpiration = TimeSpan.FromHours(1),
            MaxDeliveryAttempts = 5,
            Serializer = new SystemTextJsonSerializer()
        };

        configureOptions?.Invoke(options);

        return new EntityFrameworkQueueBroker<TestQueueDbContext>(context, options, controlState ?? new QueueBrokerControlState());
    }

    private static string CreateQueueName(Type messageType)
    {
        return string.Concat("q-", messageType.PrettyName(false), "-broker");
    }

    private static QueueMessage CreateStoredMessage(
        string type = null,
        string content = null,
        int attemptCount = 0,
        DateTimeOffset? expiresOn = null)
    {
        var serializer = new SystemTextJsonSerializer();
        var message = new TestQueueMessage("payload");

        return new QueueMessage
        {
            Id = Guid.NewGuid(),
            MessageId = message.MessageId,
            QueueName = CreateQueueName(typeof(TestQueueMessage)),
            Type = type ?? typeof(TestQueueMessage).AssemblyQualifiedName,
            Content = content ?? serializer.SerializeToString(message),
            CreatedDate = DateTimeOffset.UtcNow.AddMinutes(-1),
            ExpiresOn = expiresOn,
            Status = QueueMessageStatus.Pending,
            AttemptCount = attemptCount,
            Properties = new Dictionary<string, object>()
        };
    }

    public sealed class TestQueueMessage(string value) : QueueMessageBase
    {
        public string Value { get; } = value;
    }

    private sealed class CapturingQueueMessageHandler : IQueueMessageHandler<TestQueueMessage>
    {
        public TestQueueMessage ReceivedMessage { get; private set; }

        public Task Handle(TestQueueMessage message, CancellationToken cancellationToken)
        {
            this.ReceivedMessage = message;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingQueueMessageHandler : IQueueMessageHandler<TestQueueMessage>
    {
        public Task Handle(TestQueueMessage message, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("handler failed");
        }
    }

    public class TestQueueDbContext(DbContextOptions<TestQueueDbContext> options) : DbContext(options), IQueueingContext
    {
        public DbSet<QueueMessage> QueueMessages { get; set; }
    }
}
