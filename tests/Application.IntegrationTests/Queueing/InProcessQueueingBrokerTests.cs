namespace BridgingIT.DevKit.Application.IntegrationTests.Queueing;

using BridgingIT.DevKit.Application.Queueing;
using Microsoft.Extensions.DependencyInjection;

[IntegrationTest("Application")]
public class InProcessQueueingBrokerTests
{
    [Fact]
    public async Task InProcessBroker_WhenHandlerIsRegisteredAfterWaiting_RequeuesAndProcessesMessage()
    {
        InProcessQueueMessageHandler.Reset();
        var services = QueueingBrokerTestSupport.CreateServices();
        services.AddQueueing()
            .WithInProcessBroker(new InProcessQueueBrokerConfiguration
            {
                ProcessDelay = 0,
                MessageExpiration = TimeSpan.FromMinutes(1),
                MaxDegreeOfParallelism = 1,
                EnsureOrdered = true
            });

        using var provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<IQueueBroker>();
        var brokerService = provider.GetRequiredService<IQueueBrokerService>();
        var message = new InProcessQueueMessage("requeue-me");

        await broker.Enqueue(message);

        var waitingAppeared = await QueueingBrokerTestSupport.WaitForAsync(async () =>
            (await brokerService.GetWaitingMessagesAsync()).Any(item => item.MessageId == message.MessageId));

        await broker.Subscribe<InProcessQueueMessage, InProcessQueueMessageHandler>();

        var processed = await QueueingBrokerTestSupport.WaitForAsync(() => Task.FromResult(InProcessQueueMessageHandler.Processed));
        var summary = await brokerService.GetSummaryAsync();

        waitingAppeared.ShouldBeTrue();
        processed.ShouldBeTrue();
        InProcessQueueMessageHandler.LastProcessedMessageId.ShouldBe(message.MessageId);
        summary.WaitingForHandler.ShouldBe(0);
        summary.Succeeded.ShouldBe(1);
    }

    [Fact]
    public async Task InProcessBroker_WhenQueueIsPaused_ResumeQueueProcessesPendingMessage()
    {
        InProcessQueueMessageHandler.Reset();
        var services = QueueingBrokerTestSupport.CreateServices();
        services.AddQueueing()
            .WithInProcessBroker(new InProcessQueueBrokerConfiguration
            {
                ProcessDelay = 0,
                MessageExpiration = TimeSpan.FromMinutes(1),
                MaxDegreeOfParallelism = 1,
                EnsureOrdered = true
            });

        using var provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<IQueueBroker>();
        var brokerService = provider.GetRequiredService<IQueueBrokerService>();

        await broker.Subscribe<InProcessQueueMessage, InProcessQueueMessageHandler>();

        var queueName = typeof(InProcessQueueMessage).PrettyName(false);

        await brokerService.PauseQueueAsync(queueName);

        var message = new InProcessQueueMessage("pause-me");
        await broker.Enqueue(message);

        var pendingWhilePaused = await QueueingBrokerTestSupport.WaitForAsync(async () =>
        {
            var stored = (await brokerService.GetMessagesAsync(messageId: message.MessageId, isArchived: null)).SingleOrDefault();
            return stored?.Status == QueueMessageStatus.Pending;
        });

        var pausedSummary = await brokerService.GetSummaryAsync();

        await brokerService.ResumeQueueAsync(queueName);

        var processed = await QueueingBrokerTestSupport.WaitForAsync(() => Task.FromResult(InProcessQueueMessageHandler.ProcessCount == 1));
        var resumedSummary = await brokerService.GetSummaryAsync();

        pendingWhilePaused.ShouldBeTrue();
        pausedSummary.PausedQueues.ShouldContain(queueName);
        processed.ShouldBeTrue();
        resumedSummary.PausedQueues.ShouldNotContain(queueName);
        resumedSummary.Pending.ShouldBe(0);
        resumedSummary.Succeeded.ShouldBe(1);
    }

    [Fact]
    public async Task InProcessBroker_WhenArchivedMessageIsRetried_UnarchivesAndProcessesAgain()
    {
        InProcessQueueMessageHandler.Reset();
        var services = QueueingBrokerTestSupport.CreateServices();
        services.AddQueueing()
            .WithInProcessBroker(new InProcessQueueBrokerConfiguration
            {
                ProcessDelay = 0,
                MessageExpiration = TimeSpan.FromMinutes(1),
                MaxDegreeOfParallelism = 1,
                EnsureOrdered = true
            });

        using var provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<IQueueBroker>();
        var brokerService = provider.GetRequiredService<IQueueBrokerService>();
        var message = new InProcessQueueMessage("archive-me");

        await broker.Subscribe<InProcessQueueMessage, InProcessQueueMessageHandler>();
        await broker.Enqueue(message);

        var processedInitially = await QueueingBrokerTestSupport.WaitForAsync(() => Task.FromResult(InProcessQueueMessageHandler.ProcessCount == 1));
        processedInitially.ShouldBeTrue();

        var storedMessage = (await brokerService.GetMessagesAsync(messageId: message.MessageId, isArchived: null)).Single();

        await brokerService.ArchiveMessageAsync(storedMessage.Id);

        var archivedMessage = await brokerService.GetMessageAsync(storedMessage.Id);
        var archivedStats = await brokerService.GetMessageStatsAsync(isArchived: true);

        await brokerService.RetryMessageAsync(storedMessage.Id);

        var processedAgain = await QueueingBrokerTestSupport.WaitForAsync(() => Task.FromResult(InProcessQueueMessageHandler.ProcessCount == 2));
        var retriedMessage = await brokerService.GetMessageAsync(storedMessage.Id);

        archivedMessage.ShouldNotBeNull();
        archivedMessage.IsArchived.ShouldBeTrue();
        archivedStats.Archived.ShouldBe(1);
        processedAgain.ShouldBeTrue();
        retriedMessage.ShouldNotBeNull();
        retriedMessage.Status.ShouldBe(QueueMessageStatus.Succeeded);
        retriedMessage.IsArchived.ShouldBeFalse();
        retriedMessage.ArchivedDate.ShouldBeNull();
        retriedMessage.AttemptCount.ShouldBe(2);
    }
}