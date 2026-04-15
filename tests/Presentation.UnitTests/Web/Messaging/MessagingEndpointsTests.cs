// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Messaging;

using System.Net;
using System.Net.Http.Json;
using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Messaging;
using BridgingIT.DevKit.Presentation.Web.Messaging.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class MessagingEndpointsApplication : WebApplicationFactory<MessagingEndpointsTests>
{
    public IMessageBrokerService MessageBrokerService { get; } = Substitute.For<IMessageBrokerService>();

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var appBuilder = WebApplication.CreateBuilder();
        appBuilder.WebHost.UseTestServer();

        appBuilder.Services.AddRouting();
        appBuilder.Services.AddLogging();
        appBuilder.Services.AddSingleton(this.MessageBrokerService);
        appBuilder.Services.AddSingleton(new MessagingEndpointsOptions());
        appBuilder.Services.AddMessagingEndpoints();

        var app = appBuilder.Build();
        app.UseRouting();
        app.MapEndpoints();
        app.Start();

        return app;
    }
}

public class MessagingEndpointsTests : IAsyncDisposable
{
    private readonly MessagingEndpointsApplication factory;
    private readonly HttpClient client;
    private readonly IMessageBrokerService messageBrokerService;

    public MessagingEndpointsTests()
    {
        this.factory = new MessagingEndpointsApplication();
        this.client = this.factory.CreateClient();
        this.messageBrokerService = this.factory.MessageBrokerService;
    }

    public async ValueTask DisposeAsync()
    {
        await this.factory.DisposeAsync();
    }

    [Fact]
    public async Task GetMessages_ShouldReturnBrokerMessages()
    {
        // Arrange
        var createdDate = DateTimeOffset.UtcNow;
        this.messageBrokerService.GetMessagesAsync(
                BrokerMessageStatus.Pending,
                "OrderSubmitted",
                "msg-1",
                "node-a",
                false,
                null,
                null,
                true,
                25,
                Arg.Any<CancellationToken>())
            .Returns(
            [
                new BrokerMessageInfo
                {
                    Id = Guid.NewGuid(),
                    MessageId = "msg-1",
                    Type = "OrderSubmitted",
                    Status = BrokerMessageStatus.Pending,
                    CreatedDate = createdDate,
                    Handlers =
                    [
                        new BrokerMessageHandlerInfo
                        {
                            HandlerType = "HandlerA",
                            Status = BrokerMessageHandlerStatus.Pending
                        }
                    ]
                }
            ]);

        // Act
        var response = await this.client.GetAsync("/api/_system/messaging/messages?status=Pending&type=OrderSubmitted&messageId=msg-1&lockedBy=node-a&includeHandlers=true&take=25");
        var result = await response.Content.ReadFromJsonAsync<List<BrokerMessageInfo>>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].MessageId.ShouldBe("msg-1");
    }

    [Fact]
    public async Task GetMessageContent_ShouldReturnStoredPayload()
    {
        // Arrange
        var id = Guid.NewGuid();
        this.messageBrokerService.GetMessageContentAsync(id, Arg.Any<CancellationToken>())
            .Returns(new BrokerMessageContentInfo
            {
                Id = id,
                MessageId = "msg-2",
                Type = "OrderSubmitted",
                Content = "{\"value\":42}",
                ContentHash = "hash",
                CreatedDate = DateTimeOffset.UtcNow,
                IsArchived = false
            });

        // Act
        var response = await this.client.GetAsync($"/api/_system/messaging/messages/{id}/content");
        var result = await response.Content.ReadFromJsonAsync<BrokerMessageContentInfo>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Id.ShouldBe(id);
        result.Content.ShouldBe("{\"value\":42}");
    }

    [Fact]
    public async Task RetryMessageHandler_ShouldInvokeService()
    {
        // Arrange
        var id = Guid.NewGuid();
        this.messageBrokerService.GetMessageAsync(id, true, Arg.Any<CancellationToken>())
            .Returns(new BrokerMessageInfo
            {
                Id = id,
                MessageId = "msg-3",
                Handlers =
                [
                    new BrokerMessageHandlerInfo
                    {
                        HandlerType = "MyApp.Messages.OrderSubmittedHandler",
                        Status = BrokerMessageHandlerStatus.DeadLettered
                    }
                ]
            });

        // Act
        var response = await this.client.PostAsJsonAsync(
            $"/api/_system/messaging/messages/{id}/handlers/retry",
            new RetryBrokerMessageHandlerModel { HandlerType = "MyApp.Messages.OrderSubmittedHandler" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await this.messageBrokerService.Received(1)
            .RetryMessageHandlerAsync(id, "MyApp.Messages.OrderSubmittedHandler", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RetryMessage_ShouldReturnConflict_WhenNoRetryableHandlersExist()
    {
        // Arrange
        var id = Guid.NewGuid();
        this.messageBrokerService.GetMessageAsync(id, true, Arg.Any<CancellationToken>())
            .Returns(new BrokerMessageInfo
            {
                Id = id,
                MessageId = "msg-ok",
                Handlers =
                [
                    new BrokerMessageHandlerInfo
                    {
                        HandlerType = "HandlerA",
                        Status = BrokerMessageHandlerStatus.Succeeded
                    }
                ]
            });

        // Act
        var response = await this.client.PostAsync($"/api/_system/messaging/messages/{id}/retry", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await this.messageBrokerService.DidNotReceive().RetryMessageAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RetryMessageHandler_ShouldReturnNotFound_WhenHandlerDoesNotExist()
    {
        // Arrange
        var id = Guid.NewGuid();
        this.messageBrokerService.GetMessageAsync(id, true, Arg.Any<CancellationToken>())
            .Returns(new BrokerMessageInfo
            {
                Id = id,
                MessageId = "msg-4",
                Handlers =
                [
                    new BrokerMessageHandlerInfo
                    {
                        HandlerType = "Existing.Handler",
                        Status = BrokerMessageHandlerStatus.DeadLettered
                    }
                ]
            });

        // Act
        var response = await this.client.PostAsJsonAsync(
            $"/api/_system/messaging/messages/{id}/handlers/retry",
            new RetryBrokerMessageHandlerModel { HandlerType = "Missing.Handler" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await this.messageBrokerService.DidNotReceive()
            .RetryMessageHandlerAsync(id, "Missing.Handler", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ArchiveMessage_ShouldReturnConflict_WhenMessageIsNotTerminal()
    {
        // Arrange
        var id = Guid.NewGuid();
        this.messageBrokerService.GetMessageAsync(id, false, Arg.Any<CancellationToken>())
            .Returns(new BrokerMessageInfo
            {
                Id = id,
                MessageId = "msg-archive",
                Status = BrokerMessageStatus.Processing,
                IsArchived = false
            });

        // Act
        var response = await this.client.PostAsync($"/api/_system/messaging/messages/{id}/archive", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await this.messageBrokerService.DidNotReceive().ArchiveMessageAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PurgeMessages_ShouldPassFiltersToService()
    {
        // Arrange
        var olderThan = DateTimeOffset.UtcNow.AddDays(-7);
        var expectedStatuses = new[] { BrokerMessageStatus.Succeeded, BrokerMessageStatus.Expired };

        // Act
        var response = await this.client.DeleteAsync(
            $"/api/_system/messaging/messages?olderThan={Uri.EscapeDataString(olderThan.ToString("O"))}&statuses=Succeeded&statuses=Expired&isArchived=true");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await this.messageBrokerService.Received(1).PurgeMessagesAsync(
            Arg.Is<DateTimeOffset?>(value => value.HasValue && value.Value.ToString("O") == olderThan.ToString("O")),
            Arg.Is<IEnumerable<BrokerMessageStatus>>(value => value.SequenceEqual(expectedStatuses)),
            true,
            Arg.Any<CancellationToken>());
    }
}