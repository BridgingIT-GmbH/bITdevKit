// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.Queueing;

using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Queueing;
using Microsoft.EntityFrameworkCore;

public class EntityFrameworkQueueBrokerServiceTests
{
    [Fact]
    public async Task GetSummaryAsync_WhenMessagesExist_ReturnsPersistedCountsAndPausedState()
    {
        // Arrange
        await using var context = CreateContext();
        context.QueueMessages.AddRange(
            CreateQueueMessage("pending", QueueMessageStatus.Pending, DateTimeOffset.UtcNow.AddMinutes(-2)),
            CreateQueueMessage("waiting", QueueMessageStatus.WaitingForHandler, DateTimeOffset.UtcNow.AddMinutes(-1)));
        await context.SaveChangesAsync();

        var options = CreateOptions();
        var registrationStore = new QueueingRegistrationStore();
        var controlState = new QueueBrokerControlState();
        controlState.PauseQueue(CreateQueueName(options, typeof(TestQueueMessage)));
        var sut = new EntityFrameworkQueueBrokerService<TestQueueDbContext>(context, options, registrationStore, controlState);

        // Act
        var result = await sut.GetSummaryAsync();

        // Assert
        result.Total.ShouldBe(2);
        result.Pending.ShouldBe(1);
        result.WaitingForHandler.ShouldBe(1);
        result.PausedQueues.ShouldContain(CreateQueueName(options, typeof(TestQueueMessage)));
        result.Capabilities.SupportsDurableStorage.ShouldBeTrue();
    }

    [Fact]
    public async Task GetSubscriptionsAsync_WhenRegistrationExists_MapsQueueNameAndPauseFlags()
    {
        // Arrange
        await using var context = CreateContext();
        var options = CreateOptions();
        var registrationStore = new QueueingRegistrationStore();
        var controlState = new QueueBrokerControlState();
        registrationStore.Add(typeof(TestQueueMessage), typeof(TestQueueMessageHandler));
        controlState.PauseMessageType(typeof(TestQueueMessage).PrettyName(false));
        var sut = new EntityFrameworkQueueBrokerService<TestQueueDbContext>(context, options, registrationStore, controlState);

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
        await using var context = CreateContext();
        context.QueueMessages.AddRange(
            CreateQueueMessage("older", QueueMessageStatus.WaitingForHandler, DateTimeOffset.UtcNow.AddMinutes(-5)),
            CreateQueueMessage("newer", QueueMessageStatus.WaitingForHandler, DateTimeOffset.UtcNow));
        await context.SaveChangesAsync();

        var sut = new EntityFrameworkQueueBrokerService<TestQueueDbContext>(context, CreateOptions(), new QueueingRegistrationStore(), new QueueBrokerControlState());

        // Act
        var result = (await sut.GetWaitingMessagesAsync(1)).ToList();

        // Assert
        result.Count.ShouldBe(1);
        result[0].MessageId.ShouldBe("older");
    }

    [Fact]
    public async Task PauseAndResumeQueueAsync_WhenCalled_UpdatesControlStateVisibleThroughSummary()
    {
        // Arrange
        await using var context = CreateContext();
        var options = CreateOptions();
        var controlState = new QueueBrokerControlState();
        var queueName = CreateQueueName(options, typeof(TestQueueMessage));
        var sut = new EntityFrameworkQueueBrokerService<TestQueueDbContext>(context, options, new QueueingRegistrationStore(), controlState);

        // Act
        await sut.PauseQueueAsync(queueName);
        var pausedSummary = await sut.GetSummaryAsync();
        await sut.ResumeQueueAsync(queueName);
        var resumedSummary = await sut.GetSummaryAsync();

        // Assert
        pausedSummary.PausedQueues.ShouldContain(queueName);
        resumedSummary.PausedQueues.ShouldNotContain(queueName);
    }

    [Fact]
    public async Task ArchiveMessageAsync_WhenMessageExists_MarksMessageAsArchived()
    {
        // Arrange
        await using var context = CreateContext();
        var message = CreateQueueMessage("archive-me", QueueMessageStatus.Succeeded, DateTimeOffset.UtcNow.AddMinutes(-1));
        context.QueueMessages.Add(message);
        await context.SaveChangesAsync();

        var sut = new EntityFrameworkQueueBrokerService<TestQueueDbContext>(context, CreateOptions(), new QueueingRegistrationStore(), new QueueBrokerControlState());

        // Act
        await sut.ArchiveMessageAsync(message.Id);
        var result = await sut.GetMessageAsync(message.Id);

        // Assert
        result.ShouldNotBeNull();
        result.IsArchived.ShouldBeTrue();
        result.ArchivedDate.ShouldNotBeNull();
    }

    [Fact]
    public async Task RetryMessageAsync_WhenMessageExists_ResetsProcessingFields()
    {
        // Arrange
        await using var context = CreateContext();
        var message = CreateQueueMessage("retry-me", QueueMessageStatus.Failed, DateTimeOffset.UtcNow.AddMinutes(-1));
        message.AttemptCount = 3;
        message.LastError = "boom";
        message.IsArchived = true;
        message.ArchivedDate = DateTimeOffset.UtcNow.AddMinutes(-1);
        message.LockedBy = "worker-1";
        message.LockedUntil = DateTimeOffset.UtcNow.AddMinutes(1);
        context.QueueMessages.Add(message);
        await context.SaveChangesAsync();

        var sut = new EntityFrameworkQueueBrokerService<TestQueueDbContext>(context, CreateOptions(), new QueueingRegistrationStore(), new QueueBrokerControlState());

        // Act
        await sut.RetryMessageAsync(message.Id);
        var result = await sut.GetMessageAsync(message.Id);

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe(QueueMessageStatus.Pending);
        result.AttemptCount.ShouldBe(0);
        result.IsArchived.ShouldBeFalse();
        result.LockedBy.ShouldBeNull();
        result.LastError.ShouldBeNull();
    }

    private static TestQueueDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestQueueDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new TestQueueDbContext(options);
    }

    private static EntityFrameworkQueueBrokerOptions CreateOptions()
    {
        return new EntityFrameworkQueueBrokerOptions
        {
            QueueNamePrefix = "q-",
            QueueNameSuffix = "-broker"
        };
    }

    private static string CreateQueueName(EntityFrameworkQueueBrokerOptions options, Type messageType)
    {
        return string.Concat(options.QueueNamePrefix, messageType.PrettyName(false), options.QueueNameSuffix);
    }

    private static QueueMessage CreateQueueMessage(string messageId, QueueMessageStatus status, DateTimeOffset createdDate)
    {
        return new QueueMessage
        {
            Id = Guid.NewGuid(),
            MessageId = messageId,
            QueueName = CreateQueueName(CreateOptions(), typeof(TestQueueMessage)),
            Type = typeof(TestQueueMessage).PrettyName(false),
            Content = "{}",
            CreatedDate = createdDate,
            Status = status,
            AttemptCount = 0
        };
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

    public class TestQueueDbContext(DbContextOptions<TestQueueDbContext> options) : DbContext(options), IQueueingContext
    {
        public DbSet<QueueMessage> QueueMessages { get; set; }
    }
}