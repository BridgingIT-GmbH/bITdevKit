// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Queueing;

using BridgingIT.DevKit.Application.Queueing;

public class InProcessQueueBrokerServiceTests
{
    [Fact]
    public async Task GetSummaryAsync_WhenRuntimeContainsWaitingMessage_ReturnsCountsAndPausedState()
    {
        // Arrange
        var options = CreateOptions();
        var runtime = new InProcessQueueBrokerRuntime(options);
        var registrationStore = new QueueingRegistrationStore();
        var controlState = new QueueBrokerControlState();
        var sut = new InProcessQueueBrokerService(runtime, options, registrationStore, controlState);
        var item = CreateTrackedItem("msg-1", CreateQueueName(options, typeof(TestQueueMessage)), typeof(TestQueueMessage).PrettyName(false), DateTimeOffset.UtcNow.AddMinutes(-1));

        await runtime.EnqueueAsync(item);
        runtime.MarkWaitingForHandler(item);
        controlState.PauseQueue(item.QueueName);

        // Act
        var result = await sut.GetSummaryAsync();

        // Assert
        result.Total.ShouldBe(1);
        result.WaitingForHandler.ShouldBe(1);
        result.PausedQueues.ShouldContain(item.QueueName);
        result.Capabilities.SupportsDurableStorage.ShouldBeFalse();
    }

    [Fact]
    public async Task GetSubscriptionsAsync_WhenRegistrationExists_MapsQueueNameAndPauseFlags()
    {
        // Arrange
        var options = CreateOptions();
        var runtime = new InProcessQueueBrokerRuntime(options);
        var registrationStore = new QueueingRegistrationStore();
        var controlState = new QueueBrokerControlState();
        registrationStore.Add(typeof(TestQueueMessage), typeof(TestQueueMessageHandler));
        controlState.PauseMessageType(typeof(TestQueueMessage).PrettyName(false));
        var sut = new InProcessQueueBrokerService(runtime, options, registrationStore, controlState);

        // Act
        var result = (await sut.GetSubscriptionsAsync()).ToList();

        // Assert
        result.Count.ShouldBe(1);
        result[0].QueueName.ShouldBe(CreateQueueName(options, typeof(TestQueueMessage)));
        result[0].HandlerType.ShouldBe(typeof(TestQueueMessageHandler).FullName);
        result[0].IsMessageTypePaused.ShouldBeTrue();
    }

    [Fact]
    public async Task GetWaitingMessagesAsync_WhenTakeIsSpecified_ReturnsLimitedOldestMessages()
    {
        // Arrange
        var options = CreateOptions();
        var runtime = new InProcessQueueBrokerRuntime(options);
        var registrationStore = new QueueingRegistrationStore();
        var controlState = new QueueBrokerControlState();
        var sut = new InProcessQueueBrokerService(runtime, options, registrationStore, controlState);
        var older = CreateTrackedItem("older", CreateQueueName(options, typeof(TestQueueMessage)), typeof(TestQueueMessage).PrettyName(false), DateTimeOffset.UtcNow.AddMinutes(-5));
        var newer = CreateTrackedItem("newer", CreateQueueName(options, typeof(TestQueueMessage)), typeof(TestQueueMessage).PrettyName(false), DateTimeOffset.UtcNow);

        await runtime.EnqueueAsync(older);
        runtime.MarkWaitingForHandler(older);
        await runtime.EnqueueAsync(newer);
        runtime.MarkWaitingForHandler(newer);

        // Act
        var result = (await sut.GetWaitingMessagesAsync(1)).ToList();

        // Assert
        result.Count.ShouldBe(1);
        result[0].MessageId.ShouldBe("older");
    }

    [Fact]
    public async Task ResumeQueueAsync_WhenPausedItemExists_ClearsPauseStateAndRequeuesItem()
    {
        // Arrange
        var options = CreateOptions();
        var runtime = new InProcessQueueBrokerRuntime(options);
        var registrationStore = new QueueingRegistrationStore();
        var controlState = new QueueBrokerControlState();
        var sut = new InProcessQueueBrokerService(runtime, options, registrationStore, controlState);
        var queueName = CreateQueueName(options, typeof(TestQueueMessage));
        var item = CreateTrackedItem("msg-1", queueName, typeof(TestQueueMessage).PrettyName(false), DateTimeOffset.UtcNow);

        await runtime.EnqueueAsync(item);
        runtime.MarkPaused(item);
        controlState.PauseQueue(queueName);

        // Act
        await sut.ResumeQueueAsync(queueName);
        var result = await sut.GetSummaryAsync();

        // Assert
        controlState.IsQueuePaused(queueName).ShouldBeFalse();
        result.Pending.ShouldBe(1);
        result.PausedQueues.ShouldNotContain(queueName);
    }

    [Fact]
    public async Task GetMessageContentAsync_WhenMessageExists_ReturnsSerializedContent()
    {
        // Arrange
        var options = CreateOptions();
        var runtime = new InProcessQueueBrokerRuntime(options);
        var sut = new InProcessQueueBrokerService(runtime, options, new QueueingRegistrationStore(), new QueueBrokerControlState());
        var item = CreateTrackedItem("msg-1", CreateQueueName(options, typeof(TestQueueMessage)), typeof(TestQueueMessage).PrettyName(false), DateTimeOffset.UtcNow);

        await runtime.EnqueueAsync(item);

        // Act
        var result = await sut.GetMessageContentAsync(item.Id);

        // Assert
        result.ShouldNotBeNull();
        result.MessageId.ShouldBe("msg-1");
        result.Content.ShouldContain("msg-1");
        result.ContentHash.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RetryMessageAsync_WhenMessageWasArchivedAndFailed_ReactivatesMessage()
    {
        // Arrange
        var options = CreateOptions();
        var runtime = new InProcessQueueBrokerRuntime(options);
        var sut = new InProcessQueueBrokerService(runtime, options, new QueueingRegistrationStore(), new QueueBrokerControlState());
        var item = CreateTrackedItem("msg-1", CreateQueueName(options, typeof(TestQueueMessage)), typeof(TestQueueMessage).PrettyName(false), DateTimeOffset.UtcNow);

        await runtime.EnqueueAsync(item);
        runtime.MarkFailed(item, "boom", typeof(TestQueueMessageHandler).FullName);
        runtime.ArchiveMessage(item.Id);

        // Act
        await sut.RetryMessageAsync(item.Id);
        var result = await sut.GetMessageAsync(item.Id);

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe(QueueMessageStatus.Pending);
        result.IsArchived.ShouldBeFalse();
        result.LastError.ShouldBeNull();
    }

    private static InProcessQueueBrokerOptions CreateOptions()
    {
        return new InProcessQueueBrokerOptions
        {
            QueueNamePrefix = "q-",
            QueueNameSuffix = "-broker",
            MaxDegreeOfParallelism = 1,
            Serializer = new SystemTextJsonSerializer()
        };
    }

    private static InProcessQueueTrackedItem CreateTrackedItem(string messageId, string queueName, string type, DateTimeOffset createdDate)
    {
        var message = new TestQueueMessage(messageId) { MessageId = messageId, Timestamp = createdDate };

        return new InProcessQueueTrackedItem
        {
            Id = Guid.NewGuid(),
            Message = message,
            QueueName = queueName,
            Type = type,
            CreatedDate = createdDate,
            Status = QueueMessageStatus.Pending
        };
    }

    private static string CreateQueueName(InProcessQueueBrokerOptions options, Type messageType)
    {
        return string.Concat(options.QueueNamePrefix, messageType.PrettyName(false), options.QueueNameSuffix);
    }

    public sealed class TestQueueMessage(string value) : QueueMessageBase
    {
        public string Value { get; } = value;
    }

    public sealed class TestQueueMessageHandler : IQueueMessageHandler<TestQueueMessage>
    {
        public Task Handle(TestQueueMessage message, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}