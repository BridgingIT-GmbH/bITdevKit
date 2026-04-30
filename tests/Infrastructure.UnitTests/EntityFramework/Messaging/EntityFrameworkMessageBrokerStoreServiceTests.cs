// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Messaging;

using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Messaging;
using Microsoft.Extensions.DependencyInjection;

public class EntityFrameworkMessageBrokerStoreServiceTests(StubDbContextFixture fixture) : IClassFixture<StubDbContextFixture>
{
    private readonly StubDbContextFixture fixture = fixture;

    [Fact]
    public async Task GetMessagesAsync_ReturnsMappedBrokerMessages()
    {
        // Arrange
        await this.ResetContext();
        using var arrangeContext = this.fixture.CreateContext();
        var message = new BrokerMessage
        {
            Id = Guid.NewGuid(),
            MessageId = "msg-1",
            Type = typeof(StubMessage).AssemblyQualifiedName,
            Content = "{}",
            CreatedDate = DateTimeOffset.UtcNow,
            Status = BrokerMessageStatus.Pending,
            HandlerStates =
            [
                new BrokerMessageHandlerState
                {
                    SubscriptionKey = "StubMessage:Handler",
                    HandlerType = "Handler",
                    Status = BrokerMessageHandlerStatus.Pending,
                    AttemptCount = 2
                }
            ]
        };
        arrangeContext.BrokerMessages.Add(message);
        await arrangeContext.SaveChangesAsync();
        var sut = this.CreateSut();

        // Act
        var result = (await sut.GetMessagesAsync(includeHandlers: true)).Single();

        // Assert
        result.Id.ShouldBe(message.Id);
        result.Status.ShouldBe(BrokerMessageStatus.Pending);
        result.AttemptCountSummary.ShouldBe(2);
        result.Handlers.Single().HandlerType.ShouldBe("Handler");

    }

    [Fact]
    public async Task RetryMessageHandlerAsync_ResetsOnlyRequestedHandler()
    {
        // Arrange
        await this.ResetContext();
        using var arrangeContext = this.fixture.CreateContext();
        var message = new BrokerMessage
        {
            Id = Guid.NewGuid(),
            MessageId = "msg-2",
            Type = typeof(StubMessage).AssemblyQualifiedName,
            Content = "{}",
            CreatedDate = DateTimeOffset.UtcNow,
            Status = BrokerMessageStatus.DeadLettered,
            HandlerStates =
            [
                new BrokerMessageHandlerState
                {
                    SubscriptionKey = "StubMessage:Failed",
                    HandlerType = "FailedHandler",
                    Status = BrokerMessageHandlerStatus.DeadLettered,
                    AttemptCount = 3,
                    LastError = "boom",
                    ProcessedDate = DateTimeOffset.UtcNow
                },
                new BrokerMessageHandlerState
                {
                    SubscriptionKey = "StubMessage:Succeeded",
                    HandlerType = "SucceededHandler",
                    Status = BrokerMessageHandlerStatus.Succeeded,
                    AttemptCount = 1,
                    ProcessedDate = DateTimeOffset.UtcNow
                }
            ]
        };
        arrangeContext.BrokerMessages.Add(message);
        await arrangeContext.SaveChangesAsync();
        var sut = this.CreateSut();

        // Act
        await sut.RetryMessageHandlerAsync(message.Id, "FailedHandler");

        // Assert
        using var assertContext = this.fixture.CreateContext();
        var stored = assertContext.BrokerMessages.Single();
        stored.Status.ShouldBe(BrokerMessageStatus.Pending);
        stored.HandlerStates.Single(handler => handler.HandlerType == "FailedHandler").Status.ShouldBe(BrokerMessageHandlerStatus.Pending);
        stored.HandlerStates.Single(handler => handler.HandlerType == "FailedHandler").AttemptCount.ShouldBe(0);
        stored.HandlerStates.Single(handler => handler.HandlerType == "SucceededHandler").Status.ShouldBe(BrokerMessageHandlerStatus.Succeeded);

    }

    [Fact]
    public async Task RetryMessageAsync_WithExpiredMessage_ExtendsExpirationAndResetsState()
    {
        // Arrange
        await this.ResetContext();
        var createdDate = DateTimeOffset.UtcNow.AddMinutes(-10);
        var originalExpiration = createdDate.AddMinutes(5);
        using var arrangeContext = this.fixture.CreateContext();
        var message = new BrokerMessage
        {
            Id = Guid.NewGuid(),
            MessageId = "msg-expired",
            Type = typeof(StubMessage).AssemblyQualifiedName,
            Content = "{}",
            CreatedDate = createdDate,
            ExpiresOn = originalExpiration,
            Status = BrokerMessageStatus.Expired,
            HandlerStates =
            [
                new BrokerMessageHandlerState
                {
                    SubscriptionKey = "StubMessage:Expired",
                    HandlerType = "ExpiredHandler",
                    Status = BrokerMessageHandlerStatus.Expired,
                    AttemptCount = 2,
                    LastError = "expired",
                    ProcessedDate = DateTimeOffset.UtcNow.AddMinutes(-1)
                }
            ]
        };
        arrangeContext.BrokerMessages.Add(message);
        await arrangeContext.SaveChangesAsync();
        var sut = this.CreateSut();

        // Act
        await sut.RetryMessageAsync(message.Id);

        // Assert
        using var assertContext = this.fixture.CreateContext();
        var stored = assertContext.BrokerMessages.Single();
        stored.Status.ShouldBe(BrokerMessageStatus.Pending);
        stored.ProcessedDate.ShouldBeNull();
        stored.ExpiresOn.ShouldNotBeNull();
        stored.ExpiresOn.Value.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
        stored.HandlerStates.Single().Status.ShouldBe(BrokerMessageHandlerStatus.Pending);
        stored.HandlerStates.Single().AttemptCount.ShouldBe(0);
    }

    private IMessageBrokerService CreateSut()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => this.fixture.CreateContext());
        services.AddSingleton<MessageBrokerControlState>();
        services.AddScoped<IMessageBrokerService, EntityFrameworkMessageBrokerStoreService<StubDbContext>>();
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IMessageBrokerService>();
    }

    private async Task ResetContext()
    {
        using var context = this.fixture.CreateContext();
        context.BrokerMessages.RemoveRange(context.BrokerMessages.ToList());
        await context.SaveChangesAsync();
    }
}