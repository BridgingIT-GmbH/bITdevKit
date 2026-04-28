namespace BridgingIT.DevKit.Application.UnitTests.Queueing;

using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Infrastructure.RabbitMQ;

public class RabbitMQQueueBrokerServiceTests
{
    [Fact]
    public async Task TrackEnqueued_AddsMessageToTracker()
    {
        var service = new RabbitMQQueueBrokerService();
        var message = new TestQueueMessage("test");

        service.TrackEnqueued(message, "TestQueue", "TestQueueMessage");

        var stats = await service.GetMessageStatsAsync();
        stats.Total.ShouldBe(1);
        stats.Pending.ShouldBe(1);
    }

    [Fact]
    public async Task TrackConsumed_UpdatesStatusToProcessing()
    {
        var service = new RabbitMQQueueBrokerService();
        var message = new TestQueueMessage("test");

        service.TrackEnqueued(message, "TestQueue", "TestQueueMessage");
        service.TrackConsumed(message, "TestQueue", "TestQueueMessage");

        var stats = await service.GetMessageStatsAsync();
        stats.Processing.ShouldBe(1);
        stats.Pending.ShouldBe(0);
    }

    [Fact]
    public async Task TrackResult_Succeeded_MarksMessageSucceeded()
    {
        var service = new RabbitMQQueueBrokerService();
        var message = new TestQueueMessage("test");

        service.TrackEnqueued(message, "TestQueue", "TestQueueMessage");
        service.TrackConsumed(message, "TestQueue", "TestQueueMessage");
        service.TrackResult(message.MessageId, QueueMessageStatus.Succeeded);

        var stats = await service.GetMessageStatsAsync();
        stats.Succeeded.ShouldBe(1);
        stats.Processing.ShouldBe(0);
    }

    [Fact]
    public async Task TrackResult_Failed_WithAttemptsBelowMax_KeepsTrackable()
    {
        var service = new RabbitMQQueueBrokerService();
        var message = new TestQueueMessage("test");

        service.TrackEnqueued(message, "TestQueue", "TestQueueMessage");
        service.TrackConsumed(message, "TestQueue", "TestQueueMessage");
        service.TrackResult(message.MessageId, QueueMessageStatus.Failed, 2);

        var items = await service.GetMessagesAsync(status: QueueMessageStatus.Failed);
        var item = items.Single();
        item.AttemptCount.ShouldBe(2);
        item.Status.ShouldBe(QueueMessageStatus.Failed);
    }

    [Fact]
    public async Task GetMessagesAsync_WithTypeFilter_ReturnsMatchingItems()
    {
        var service = new RabbitMQQueueBrokerService();
        var message1 = new TestQueueMessage("a");
        var message2 = new SecondTestQueueMessage("b");

        service.TrackEnqueued(message1, "QueueA", "TestQueueMessage");
        service.TrackEnqueued(message2, "QueueB", "SecondTestQueueMessage");

        var results = await service.GetMessagesAsync(type: "Second");
        results.Count().ShouldBe(1);
        results.First().Type.ShouldBe("SecondTestQueueMessage");
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsCorrectCounts()
    {
        var service = new RabbitMQQueueBrokerService();
        var message = new TestQueueMessage("test");

        service.TrackEnqueued(message, "TestQueue", "TestQueueMessage");

        var summary = await service.GetSummaryAsync();
        summary.Total.ShouldBe(1);
        summary.Pending.ShouldBe(1);
        summary.Capabilities.SupportsDurableStorage.ShouldBeFalse();
        summary.Capabilities.SupportsPauseResume.ShouldBeTrue();
    }

    [Fact]
    public async Task PauseQueueAsync_PausesQueue()
    {
        var service = new RabbitMQQueueBrokerService();

        await service.PauseQueueAsync("MyQueue");

        var summary = await service.GetSummaryAsync();
        summary.PausedQueues.ShouldContain("MyQueue");
    }

    [Fact]
    public async Task ResumeQueueAsync_ResumesQueue()
    {
        var service = new RabbitMQQueueBrokerService();
        await service.PauseQueueAsync("MyQueue");

        await service.ResumeQueueAsync("MyQueue");

        var summary = await service.GetSummaryAsync();
        summary.PausedQueues.ShouldNotContain("MyQueue");
    }

    [Fact]
    public async Task ArchiveMessageAsync_MarksItemArchived()
    {
        var service = new RabbitMQQueueBrokerService();
        var message = new TestQueueMessage("test");

        service.TrackEnqueued(message, "TestQueue", "TestQueueMessage");
        var id = (await service.GetMessagesAsync()).First().Id;

        await service.ArchiveMessageAsync(id);

        var item = await service.GetMessageAsync(id);
        item.IsArchived.ShouldBeTrue();
    }

    [Fact]
    public async Task RetryMessageAsync_ResetsFailedMessageToPending()
    {
        var service = new RabbitMQQueueBrokerService();
        var message = new TestQueueMessage("test");

        service.TrackEnqueued(message, "TestQueue", "TestQueueMessage");
        service.TrackConsumed(message, "TestQueue", "TestQueueMessage");
        service.TrackResult(message.MessageId, QueueMessageStatus.Failed, 1);

        var id = (await service.GetMessagesAsync()).First().Id;
        await service.RetryMessageAsync(id);

        var item = await service.GetMessageAsync(id);
        item.Status.ShouldBe(QueueMessageStatus.Pending);
        item.AttemptCount.ShouldBe(1); // Preserved
    }

    [Fact]
    public async Task PurgeMessagesAsync_RemovesMatchingItems()
    {
        var service = new RabbitMQQueueBrokerService();
        var message = new TestQueueMessage("test");

        service.TrackEnqueued(message, "TestQueue", "TestQueueMessage");
        var id = (await service.GetMessagesAsync()).First().Id;
        await service.ArchiveMessageAsync(id);

        await service.PurgeMessagesAsync(isArchived: true);

        var stats = await service.GetMessageStatsAsync();
        stats.Total.ShouldBe(0);
    }

    [Fact]
    public async Task GetWaitingMessagesAsync_ReturnsOnlyWaitingItems()
    {
        var service = new RabbitMQQueueBrokerService();
        var message = new TestQueueMessage("test");

        service.TrackEnqueued(message, "TestQueue", "TestQueueMessage");
        service.TrackConsumed(message, "TestQueue", "TestQueueMessage");
        service.TrackResult(message.MessageId, QueueMessageStatus.WaitingForHandler);

        var waiting = await service.GetWaitingMessagesAsync();
        waiting.Count().ShouldBe(1);
        waiting.First().Status.ShouldBe(QueueMessageStatus.WaitingForHandler);
    }

    private sealed class TestQueueMessage(string value) : QueueMessageBase
    {
        public string Value { get; } = value;
    }

    private sealed class SecondTestQueueMessage(string value) : QueueMessageBase
    {
        public string Value { get; } = value;
    }
}
