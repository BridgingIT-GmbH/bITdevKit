namespace BridgingIT.DevKit.Infrastructure.UnitTests.Azure.ServiceBus.Queueing;

using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Infrastructure.Azure;

public class ServiceBusQueueBrokerRuntimeTests
{
    private static ServiceBusQueueBrokerRuntime CreateRuntime()
    {
        return new ServiceBusQueueBrokerRuntime(new ServiceBusQueueBrokerOptions());
    }

    private static IQueueMessage CreateMessage(string messageId = "test-id")
    {
        var message = Substitute.For<IQueueMessage>();
        message.MessageId.Returns(messageId);
        message.Timestamp.Returns(DateTimeOffset.UtcNow);
        message.Properties.Returns(new Dictionary<string, object>());
        return message;
    }

    [Fact]
    public void TrackEnqueued_AddsItemToTracking()
    {
        var runtime = CreateRuntime();
        var message = CreateMessage();

        runtime.TrackEnqueued(message, "test-queue", "TestMessage");
        var summary = runtime.GetSummary(new QueueBrokerControlState());

        summary.Total.ShouldBe(1);
        summary.Pending.ShouldBe(1);
    }

    [Fact]
    public void TrackSucceeded_UpdatesItemStatus()
    {
        var runtime = CreateRuntime();
        var message = CreateMessage();

        runtime.TrackEnqueued(message, "test-queue", "TestMessage");
        runtime.TrackSucceeded(message, "test-queue", "TestMessage");
        var summary = runtime.GetSummary(new QueueBrokerControlState());

        summary.Succeeded.ShouldBe(1);
        summary.Pending.ShouldBe(0);
    }

    [Fact]
    public void TrackFailed_UpdatesItemStatusAndIncrementsAttemptCount()
    {
        var runtime = CreateRuntime();
        var message = CreateMessage();

        runtime.TrackEnqueued(message, "test-queue", "TestMessage");
        runtime.TrackFailed(message, "test-queue", "TestMessage");
        var messages = runtime.GetMessages(status: QueueMessageStatus.Failed).ToList();

        messages.Count.ShouldBe(1);
        messages[0].AttemptCount.ShouldBe(1);
    }

    [Fact]
    public void TrackWaitingForHandler_UpdatesItemStatus()
    {
        var runtime = CreateRuntime();
        var message = CreateMessage();

        runtime.TrackEnqueued(message, "test-queue", "TestMessage");
        runtime.TrackWaitingForHandler(message, "test-queue", "TestMessage");
        var summary = runtime.GetSummary(new QueueBrokerControlState());

        summary.WaitingForHandler.ShouldBe(1);
        summary.Pending.ShouldBe(0);
    }

    [Fact]
    public void TrackExpired_UpdatesItemStatus()
    {
        var runtime = CreateRuntime();
        var message = CreateMessage();

        runtime.TrackEnqueued(message, "test-queue", "TestMessage");
        runtime.TrackExpired(message, "test-queue", "TestMessage");
        var summary = runtime.GetSummary(new QueueBrokerControlState());

        summary.Expired.ShouldBe(1);
        summary.Pending.ShouldBe(0);
    }

    [Fact]
    public void GetMessages_WithStatusFilter_ReturnsMatchingItems()
    {
        var runtime = CreateRuntime();
        var message1 = CreateMessage("msg-1");
        var message2 = CreateMessage("msg-2");

        runtime.TrackEnqueued(message1, "queue-1", "TypeA");
        runtime.TrackSucceeded(message1, "queue-1", "TypeA");
        runtime.TrackEnqueued(message2, "queue-2", "TypeB");
        runtime.TrackFailed(message2, "queue-2", "TypeB");

        var succeeded = runtime.GetMessages(status: QueueMessageStatus.Succeeded).ToList();
        var failed = runtime.GetMessages(status: QueueMessageStatus.Failed).ToList();

        succeeded.Count.ShouldBe(1);
        failed.Count.ShouldBe(1);
    }

    [Fact]
    public void GetMessages_WithTypeFilter_ReturnsMatchingItems()
    {
        var runtime = CreateRuntime();
        var message = CreateMessage();

        runtime.TrackEnqueued(message, "queue-1", "TypeA");

        var matching = runtime.GetMessages(type: "TypeA").ToList();
        var nonMatching = runtime.GetMessages(type: "TypeB").ToList();

        matching.Count.ShouldBe(1);
        nonMatching.Count.ShouldBe(0);
    }

    [Fact]
    public void RetryMessage_ResetsItemToPending()
    {
        var runtime = CreateRuntime();
        var message = CreateMessage();

        runtime.TrackEnqueued(message, "test-queue", "TestMessage");
        runtime.TrackFailed(message, "test-queue", "TestMessage");
        var failedMessage = runtime.GetMessages(status: QueueMessageStatus.Failed).First();

        var result = runtime.RetryMessage(failedMessage.Id);
        var retried = runtime.GetMessage(failedMessage.Id);

        result.ShouldBeTrue();
        retried.Status.ShouldBe(QueueMessageStatus.Pending);
        retried.AttemptCount.ShouldBe(0);
    }

    [Fact]
    public void RetryMessage_NonExistentId_ReturnsFalse()
    {
        var runtime = CreateRuntime();

        var result = runtime.RetryMessage(Guid.NewGuid());

        result.ShouldBeFalse();
    }

    [Fact]
    public void ArchiveMessage_SetsArchivedState()
    {
        var runtime = CreateRuntime();
        var message = CreateMessage();

        runtime.TrackEnqueued(message, "test-queue", "TestMessage");
        runtime.TrackSucceeded(message, "test-queue", "TestMessage");
        var tracked = runtime.GetMessages().First();

        var result = runtime.ArchiveMessage(tracked.Id);
        var archived = runtime.GetMessage(tracked.Id);

        result.ShouldBeTrue();
        archived.IsArchived.ShouldBeTrue();
        archived.ArchivedDate.ShouldNotBeNull();
    }

    [Fact]
    public void PurgeMessages_RemovesMatchingItems()
    {
        var runtime = CreateRuntime();
        var message = CreateMessage();

        runtime.TrackEnqueued(message, "test-queue", "TestMessage");
        runtime.TrackSucceeded(message, "test-queue", "TestMessage");

        var count = runtime.PurgeMessages(statuses: new[] { QueueMessageStatus.Succeeded });
        var summary = runtime.GetSummary(new QueueBrokerControlState());

        count.ShouldBe(1);
        summary.Total.ShouldBe(0);
    }

    [Fact]
    public void GetSummary_IncludesCapabilities()
    {
        var runtime = CreateRuntime();
        var summary = runtime.GetSummary(new QueueBrokerControlState());

        summary.Capabilities.ShouldNotBeNull();
        summary.Capabilities.SupportsDurableStorage.ShouldBeTrue();
        summary.Capabilities.SupportsPauseResume.ShouldBeTrue();
    }

    [Fact]
    public void GetWaitingMessages_ReturnsOnlyWaitingItems()
    {
        var runtime = CreateRuntime();
        var message1 = CreateMessage("msg-1");
        var message2 = CreateMessage("msg-2");

        runtime.TrackEnqueued(message1, "queue-1", "TypeA");
        runtime.TrackWaitingForHandler(message1, "queue-1", "TypeA");
        runtime.TrackEnqueued(message2, "queue-2", "TypeB");
        runtime.TrackSucceeded(message2, "queue-2", "TypeB");

        var waiting = runtime.GetWaitingMessages().ToList();

        waiting.Count.ShouldBe(1);
        waiting[0].MessageId.ShouldBe("msg-1");
    }

    [Fact]
    public void GetMessageStats_ReturnsAggregatedCounts()
    {
        var runtime = CreateRuntime();
        var message = CreateMessage();

        runtime.TrackEnqueued(message, "test-queue", "TestMessage");
        runtime.TrackSucceeded(message, "test-queue", "TestMessage");

        var stats = runtime.GetMessageStats(new QueueBrokerControlState());

        stats.Total.ShouldBe(1);
        stats.Succeeded.ShouldBe(1);
    }
}
