// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using System.Net;
using System.Net.Http.Json;
using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Queueing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class QueueingEndpointsApplication : WebApplicationFactory<QueueingEndpointsTests>
{
    public IQueueBrokerService QueueBrokerService { get; } = Substitute.For<IQueueBrokerService>();

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var appBuilder = WebApplication.CreateBuilder();
        appBuilder.WebHost.UseTestServer();

        appBuilder.Services.AddRouting();
        appBuilder.Services.AddLogging();
        appBuilder.Services.AddSingleton(this.QueueBrokerService);
        appBuilder.Services.AddSingleton(new QueueingEndpointsOptions());
        appBuilder.Services.AddQueueingEndpoints();

        var app = appBuilder.Build();
        app.UseRouting();
        app.MapEndpoints();
        app.Start();

        return app;
    }
}

public class QueueingEndpointsTests : IAsyncDisposable
{
    private readonly QueueingEndpointsApplication factory;
    private readonly HttpClient client;
    private readonly IQueueBrokerService queueBrokerService;

    public QueueingEndpointsTests()
    {
        this.factory = new QueueingEndpointsApplication();
        this.client = this.factory.CreateClient();
        this.queueBrokerService = this.factory.QueueBrokerService;
    }

    public async ValueTask DisposeAsync()
    {
        await this.factory.DisposeAsync();
    }

    [Fact]
    public async Task GetSummary_ShouldReturnQueueBrokerSummary()
    {
        // Arrange
        this.queueBrokerService.GetSummaryAsync(Arg.Any<CancellationToken>())
            .Returns(new QueueBrokerSummary
            {
                Total = 10,
                WaitingForHandler = 2,
                Capabilities = new QueueBrokerCapabilities { SupportsDurableStorage = true }
            });

        // Act
        var response = await this.client.GetAsync("/api/_system/queueing/stats");
        var result = await response.Content.ReadFromJsonAsync<QueueBrokerSummary>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Total.ShouldBe(10);
        result.WaitingForHandler.ShouldBe(2);
        result.Capabilities.SupportsDurableStorage.ShouldBeTrue();
    }

    [Fact]
    public async Task GetSubscriptions_ShouldReturnQueueSubscriptions()
    {
        // Arrange
        this.queueBrokerService.GetSubscriptionsAsync(Arg.Any<CancellationToken>())
            .Returns(
            [
                new QueueSubscriptionInfo
                {
                    QueueName = "OrderQueue",
                    MessageType = "OrderQueuedMessage",
                    HandlerType = "OrderQueuedHandler"
                }
            ]);

        // Act
        var response = await this.client.GetAsync("/api/_system/queueing/subscriptions");
        var result = await response.Content.ReadFromJsonAsync<List<QueueSubscriptionInfo>>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].QueueName.ShouldBe("OrderQueue");
    }

    [Fact]
    public async Task GetWaitingMessages_ShouldPassTakeToService()
    {
        // Arrange
        this.queueBrokerService.GetWaitingMessagesAsync(25, Arg.Any<CancellationToken>())
            .Returns(
            [
                new QueueMessageInfo
                {
                    Id = Guid.NewGuid(),
                    MessageId = "msg-1",
                    QueueName = "OrderQueue",
                    Type = "OrderQueuedMessage",
                    Status = QueueMessageStatus.WaitingForHandler,
                    CreatedDate = DateTimeOffset.UtcNow
                }
            ]);

        // Act
        var response = await this.client.GetAsync("/api/_system/queueing/messages/waiting?take=25");
        var result = await response.Content.ReadFromJsonAsync<List<QueueMessageInfo>>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        await this.queueBrokerService.Received(1).GetWaitingMessagesAsync(25, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PauseQueue_ShouldInvokeService()
    {
        // Act
        var response = await this.client.PostAsync("/api/_system/queueing/queues/orders/pause", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await this.queueBrokerService.Received(1).PauseQueueAsync("orders", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResumeMessageType_ShouldInvokeService()
    {
        // Act
        var response = await this.client.PostAsync("/api/_system/queueing/types/OrderQueuedMessage/resume", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await this.queueBrokerService.Received(1).ResumeMessageTypeAsync("OrderQueuedMessage", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetMessages_ShouldPassFiltersToService()
    {
        // Arrange
        this.queueBrokerService.GetMessagesAsync(
                QueueMessageStatus.Failed,
                "OrderQueuedMessage",
                "orders",
                "msg-1",
                null,
                false,
                null,
                null,
                10,
                Arg.Any<CancellationToken>())
            .Returns(
            [
                new QueueMessageInfo
                {
                    Id = Guid.NewGuid(),
                    MessageId = "msg-1",
                    QueueName = "orders",
                    Type = "OrderQueuedMessage",
                    Status = QueueMessageStatus.Failed,
                    CreatedDate = DateTimeOffset.UtcNow
                }
            ]);

        // Act
        var response = await this.client.GetAsync("/api/_system/queueing/messages?status=Failed&type=OrderQueuedMessage&queueName=orders&messageId=msg-1&isArchived=false&take=10");
        var result = await response.Content.ReadFromJsonAsync<List<QueueMessageInfo>>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        await this.queueBrokerService.Received(1).GetMessagesAsync(
            QueueMessageStatus.Failed,
            "OrderQueuedMessage",
            "orders",
            "msg-1",
            null,
            false,
            null,
            null,
            10,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetMessage_ShouldReturnQueueMessageDetails()
    {
        // Arrange
        var id = Guid.NewGuid();
        this.queueBrokerService.GetMessageAsync(id, Arg.Any<CancellationToken>())
            .Returns(new QueueMessageInfo
            {
                Id = id,
                MessageId = "msg-42",
                QueueName = "orders",
                Type = "OrderQueuedMessage",
                Status = QueueMessageStatus.Succeeded,
                CreatedDate = DateTimeOffset.UtcNow
            });

        // Act
        var response = await this.client.GetAsync($"/api/_system/queueing/messages/{id}");
        var result = await response.Content.ReadFromJsonAsync<QueueMessageInfo>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Id.ShouldBe(id);
        result.MessageId.ShouldBe("msg-42");
    }

    [Fact]
    public async Task GetMessageContent_ShouldReturnQueueMessagePayload()
    {
        // Arrange
        var id = Guid.NewGuid();
        this.queueBrokerService.GetMessageContentAsync(id, Arg.Any<CancellationToken>())
            .Returns(new QueueMessageContentInfo
            {
                Id = id,
                MessageId = "msg-42",
                QueueName = "orders",
                Type = "OrderQueuedMessage",
                Content = "{\"orderId\":42}",
                ContentHash = "abc123",
                CreatedDate = DateTimeOffset.UtcNow
            });

        // Act
        var response = await this.client.GetAsync($"/api/_system/queueing/messages/{id}/content");
        var result = await response.Content.ReadFromJsonAsync<QueueMessageContentInfo>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Id.ShouldBe(id);
        result.Content.ShouldBe("{\"orderId\":42}");
    }

    [Fact]
    public async Task GetMessageStats_ShouldPassArchiveFilterToService()
    {
        // Arrange
        this.queueBrokerService.GetMessageStatsAsync(null, null, true, Arg.Any<CancellationToken>())
            .Returns(new QueueMessageStats
            {
                Total = 4,
                Archived = 4
            });

        // Act
        var response = await this.client.GetAsync("/api/_system/queueing/messages/stats?isArchived=true");
        var result = await response.Content.ReadFromJsonAsync<QueueMessageStats>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Archived.ShouldBe(4);
        await this.queueBrokerService.Received(1).GetMessageStatsAsync(null, null, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RetryMessage_ShouldInvokeServiceWhenMessageIsRetryable()
    {
        // Arrange
        var id = Guid.NewGuid();
        this.queueBrokerService.GetMessageAsync(id, Arg.Any<CancellationToken>())
            .Returns(new QueueMessageInfo
            {
                Id = id,
                Status = QueueMessageStatus.Failed,
                CreatedDate = DateTimeOffset.UtcNow
            });

        // Act
        var response = await this.client.PostAsync($"/api/_system/queueing/messages/{id}/retry", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await this.queueBrokerService.Received(1).RetryMessageAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ArchiveMessage_ShouldInvokeServiceWhenMessageIsTerminal()
    {
        // Arrange
        var id = Guid.NewGuid();
        this.queueBrokerService.GetMessageAsync(id, Arg.Any<CancellationToken>())
            .Returns(new QueueMessageInfo
            {
                Id = id,
                Status = QueueMessageStatus.Succeeded,
                IsArchived = false,
                CreatedDate = DateTimeOffset.UtcNow
            });

        // Act
        var response = await this.client.PostAsync($"/api/_system/queueing/messages/{id}/archive", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await this.queueBrokerService.Received(1).ArchiveMessageAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ArchiveMessage_ShouldReturnConflictWhenMessageIsNotTerminal()
    {
        // Arrange
        var id = Guid.NewGuid();
        this.queueBrokerService.GetMessageAsync(id, Arg.Any<CancellationToken>())
            .Returns(new QueueMessageInfo
            {
                Id = id,
                Status = QueueMessageStatus.Processing,
                IsArchived = false,
                CreatedDate = DateTimeOffset.UtcNow
            });

        // Act
        var response = await this.client.PostAsync($"/api/_system/queueing/messages/{id}/archive", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await this.queueBrokerService.DidNotReceive().ArchiveMessageAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ArchiveMessage_ShouldReturnNotFoundWhenMessageDoesNotExist()
    {
        // Arrange
        var id = Guid.NewGuid();
        this.queueBrokerService.GetMessageAsync(id, Arg.Any<CancellationToken>())
            .Returns((QueueMessageInfo)null);

        // Act
        var response = await this.client.PostAsync($"/api/_system/queueing/messages/{id}/archive", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await this.queueBrokerService.DidNotReceive().ArchiveMessageAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PurgeMessages_ShouldPassFiltersToService()
    {
        // Act
        var response = await this.client.DeleteAsync("/api/_system/queueing/messages?olderThan=2026-01-01T00:00:00Z&statuses=Succeeded&statuses=Expired&isArchived=true");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await this.queueBrokerService.Received(1).PurgeMessagesAsync(
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            Arg.Is<IEnumerable<QueueMessageStatus>>(statuses => statuses.SequenceEqual(new[] { QueueMessageStatus.Succeeded, QueueMessageStatus.Expired })),
            true,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResetMessageTypeCircuit_ShouldInvokeService()
    {
        // Act
        var response = await this.client.PostAsync("/api/_system/queueing/types/OrderQueuedMessage/circuit/reset", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await this.queueBrokerService.Received(1).ResetMessageTypeCircuitAsync("OrderQueuedMessage", Arg.Any<CancellationToken>());
    }
}