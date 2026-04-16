namespace BridgingIT.DevKit.Application.IntegrationTests.Queueing;

using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Queueing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

[IntegrationTest("Application")]
public class EntityFrameworkQueueingBrokerTests
{
    [Fact]
    public async Task EntityFrameworkBroker_WhenWorkerRuns_ProcessesPersistedMessageSafely()
    {
        EntityFrameworkQueueMessageHandler.Reset();
        using var provider = this.CreateProvider();
        var broker = provider.GetRequiredService<IQueueBroker>();
        var worker = provider.GetRequiredService<EntityFrameworkQueueBrokerWorker<TestQueueDbContext>>();
        var message = new EntityFrameworkQueueMessage("persist-me");

        await broker.Subscribe<EntityFrameworkQueueMessage, EntityFrameworkQueueMessageHandler>();

        await broker.EnqueueAndWait(message);
        await worker.ProcessAsync();

        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestQueueDbContext>();
        var storedMessage = await context.QueueMessages.SingleAsync(item => item.MessageId == message.MessageId);

        storedMessage.Status.ShouldBe(QueueMessageStatus.Succeeded, storedMessage.LastError);
        storedMessage.AttemptCount.ShouldBe(1);
        storedMessage.LockedBy.ShouldBeNull();
        storedMessage.LockedUntil.ShouldBeNull();
        storedMessage.RegisteredHandlerType.ShouldBe(typeof(EntityFrameworkQueueMessageHandler).FullName);
        EntityFrameworkQueueMessageHandler.Processed.ShouldBeTrue();
    }

    [Fact]
    public async Task EntityFrameworkBroker_WhenAutoArchiveAfterIsReached_ArchivesTerminalMessage()
    {
        EntityFrameworkQueueMessageHandler.Reset();
        using var provider = this.CreateProvider(configuration =>
        {
            configuration.AutoArchiveAfter = TimeSpan.Zero;
        });

        var broker = provider.GetRequiredService<IQueueBroker>();
        var worker = provider.GetRequiredService<EntityFrameworkQueueBrokerWorker<TestQueueDbContext>>();
        var message = new EntityFrameworkQueueMessage("archive-me");

        await broker.Subscribe<EntityFrameworkQueueMessage, EntityFrameworkQueueMessageHandler>();

        await broker.EnqueueAndWait(message);
        await worker.ProcessAsync();

        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestQueueDbContext>();
        var storedMessage = await context.QueueMessages.SingleAsync(item => item.MessageId == message.MessageId);

        storedMessage.Status.ShouldBe(QueueMessageStatus.Succeeded, storedMessage.LastError);
        storedMessage.IsArchived.ShouldBeTrue();
        storedMessage.ArchivedDate.ShouldNotBeNull();
    }

    [Fact]
    public async Task EntityFrameworkBroker_WhenAutoArchiveStatusDoesNotMatch_LeavesTerminalMessageActive()
    {
        EntityFrameworkQueueMessageHandler.Reset();
        using var provider = this.CreateProvider(configuration =>
        {
            configuration.AutoArchiveAfter = TimeSpan.Zero;
            configuration.AutoArchiveStatuses = [QueueMessageStatus.DeadLettered];
        });

        var broker = provider.GetRequiredService<IQueueBroker>();
        var worker = provider.GetRequiredService<EntityFrameworkQueueBrokerWorker<TestQueueDbContext>>();
        var message = new EntityFrameworkQueueMessage("stay-active");

        await broker.Subscribe<EntityFrameworkQueueMessage, EntityFrameworkQueueMessageHandler>();

        await broker.EnqueueAndWait(message);
        await worker.ProcessAsync();

        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestQueueDbContext>();
        var storedMessage = await context.QueueMessages.SingleAsync(item => item.MessageId == message.MessageId);

        storedMessage.Status.ShouldBe(QueueMessageStatus.Succeeded, storedMessage.LastError);
        storedMessage.IsArchived.ShouldBeFalse();
        storedMessage.ArchivedDate.ShouldBeNull();
    }

    [Fact]
    public async Task EntityFrameworkBroker_GetWaitingMessagesAsync_ReturnsOldestMessagesFirst()
    {
        using var provider = this.CreateProvider();
        var brokerService = provider.GetRequiredService<IQueueBrokerService>();

        using (var scope = provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TestQueueDbContext>();
            context.QueueMessages.AddRange(
            [
                new QueueMessage
                {
                    Id = Guid.NewGuid(),
                    MessageId = "older-message",
                    QueueName = nameof(EntityFrameworkQueueMessage),
                    Type = nameof(EntityFrameworkQueueMessage),
                    Content = "{}",
                    CreatedDate = DateTimeOffset.UtcNow.AddMinutes(-5),
                    Status = QueueMessageStatus.WaitingForHandler
                },
                new QueueMessage
                {
                    Id = Guid.NewGuid(),
                    MessageId = "newer-message",
                    QueueName = nameof(EntityFrameworkQueueMessage),
                    Type = nameof(EntityFrameworkQueueMessage),
                    Content = "{}",
                    CreatedDate = DateTimeOffset.UtcNow.AddMinutes(-1),
                    Status = QueueMessageStatus.WaitingForHandler
                }
            ]);
            await context.SaveChangesAsync();
        }

        var messages = (await brokerService.GetWaitingMessagesAsync()).ToList();

        messages.Select(item => item.MessageId).ToArray().ShouldBe(["older-message", "newer-message"]);
    }

    [Fact]
    public async Task EntityFrameworkBroker_WhenRetryingArchivedMessage_ClearsOperationalState()
    {
        using var provider = this.CreateProvider();
        var brokerService = provider.GetRequiredService<IQueueBrokerService>();
        Guid id;

        using (var scope = provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TestQueueDbContext>();
            var message = new QueueMessage
            {
                Id = Guid.NewGuid(),
                MessageId = "retry-me",
                QueueName = nameof(EntityFrameworkQueueMessage),
                Type = nameof(EntityFrameworkQueueMessage),
                Content = "{}",
                CreatedDate = DateTimeOffset.UtcNow.AddMinutes(-10),
                Status = QueueMessageStatus.DeadLettered,
                AttemptCount = 3,
                RegisteredHandlerType = typeof(EntityFrameworkQueueMessageHandler).FullName,
                LastError = "boom",
                ProcessedDate = DateTimeOffset.UtcNow.AddMinutes(-5),
                ProcessingStartedDate = DateTimeOffset.UtcNow.AddMinutes(-6),
                LockedBy = "worker-1",
                LockedUntil = DateTimeOffset.UtcNow.AddMinutes(1),
                IsArchived = true,
                ArchivedDate = DateTimeOffset.UtcNow.AddMinutes(-4)
            };

            context.QueueMessages.Add(message);
            await context.SaveChangesAsync();
            id = message.Id;
        }

        await brokerService.RetryMessageAsync(id);

        using var verificationScope = provider.CreateScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<TestQueueDbContext>();
        var storedMessage = await verificationContext.QueueMessages.SingleAsync(item => item.Id == id);

        storedMessage.Status.ShouldBe(QueueMessageStatus.Pending);
        storedMessage.AttemptCount.ShouldBe(0);
        storedMessage.RegisteredHandlerType.ShouldBeNull();
        storedMessage.LastError.ShouldBeNull();
        storedMessage.ProcessedDate.ShouldBeNull();
        storedMessage.ProcessingStartedDate.ShouldBeNull();
        storedMessage.LockedBy.ShouldBeNull();
        storedMessage.LockedUntil.ShouldBeNull();
        storedMessage.IsArchived.ShouldBeFalse();
        storedMessage.ArchivedDate.ShouldBeNull();
    }

    private ServiceProvider CreateProvider(Action<EntityFrameworkQueueBrokerConfiguration> configure = null)
    {
        var services = QueueingBrokerTestSupport.CreateServices();
        var databaseRoot = new InMemoryDatabaseRoot();
        services.AddTestQueueDbContext($"queueing-{Guid.NewGuid():N}", databaseRoot);

        var configuration = new EntityFrameworkQueueBrokerConfiguration
        {
            AutoSave = true,
            StartupDelay = TimeSpan.Zero,
            ProcessingInterval = TimeSpan.FromMilliseconds(50),
            ProcessingDelay = TimeSpan.Zero,
            ProcessingCount = 10,
            LeaseDuration = TimeSpan.FromSeconds(30),
            LeaseRenewalInterval = TimeSpan.FromSeconds(10),
            MaxDeliveryAttempts = 3,
            MessageExpiration = TimeSpan.FromMinutes(5)
        };

        configure?.Invoke(configuration);

        services.AddQueueing().WithEntityFrameworkBroker<TestQueueDbContext>(configuration);

        return services.BuildServiceProvider();
    }
}