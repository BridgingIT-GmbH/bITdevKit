namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Messaging;

using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Messaging;
using Microsoft.Extensions.Logging;

public class OutboxMessagePublisherBehaviorTests : IClassFixture<StubDbContextFixture>
{
    private readonly StubDbContextFixture fixture;

    public OutboxMessagePublisherBehaviorTests(StubDbContextFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Publish_IsCalled_OutboxReceivedEvent()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var loggerFactory = Substitute.For<ILoggerFactory>();
        var message = new StubMessage() { FirstName = "John", LastName = $"Doe{ticks}" };
        var next = Substitute.For<MessagePublisherDelegate>();
        var sut = OutboxMessageWorkerBehaviorFascade<StubDbContext>.CreatePublishBehaviorForTest(loggerFactory, this.fixture.Context);
        //var sut = new OutboxMessagePublisherBehavior<StubDbContext>(loggerFactory, this.fixture.Context);

        // Act
        await sut.Publish(message, CancellationToken.None, next); // OutboxMessage are autosaved

        // Assert
        this.fixture.Context.OutboxMessages.ToList().Any(e => e.Content.Contains(message.LastName)).ShouldBeTrue();
        this.fixture.Context.OutboxMessages.ToList().Count(e => e.Content.Contains(ticks.ToString())).ShouldBe(1); // insert
    }

    [Fact]
    public async Task Publish_WithQueueIsCalled_OutboxReceivedEvent()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var loggerFactory = Substitute.For<ILoggerFactory>();
        var message = new StubMessage() { FirstName = "John", LastName = $"Doe{ticks}" };
        var messageQueue = Substitute.For<IOutboxMessageQueue>();
        var next = Substitute.For<MessagePublisherDelegate>();
        var sut = OutboxMessageWorkerBehaviorFascade<StubDbContext>.CreatePublishBehaviorForTest(loggerFactory, this.fixture.Context);

        // Act
        await sut.Publish(message, CancellationToken.None, next); // OutboxMessage are autosaved and enqueued

        // Assert
        //messageQueue.Received().Enqueue(Arg.Any<string>());
        this.fixture.Context.OutboxMessages.ToList().Any(e => e.Content.Contains(message.LastName)).ShouldBeTrue();
        this.fixture.Context.OutboxMessages.ToList().Count(e => e.Content.Contains(ticks.ToString())).ShouldBe(1); // insert
    }
}
