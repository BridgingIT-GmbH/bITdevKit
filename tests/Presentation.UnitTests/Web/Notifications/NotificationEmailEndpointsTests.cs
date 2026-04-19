// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using System.Net;
using System.Net.Http.Json;
using BridgingIT.DevKit.Application.Notifications;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Notifications;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class NotificationEmailEndpointsApplication : WebApplicationFactory<NotificationEmailEndpointsTests>
{
    public INotificationEmailOutboxService OutboxService { get; } = Substitute.For<INotificationEmailOutboxService>();

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var appBuilder = WebApplication.CreateBuilder();
        appBuilder.WebHost.UseTestServer();

        appBuilder.Services.AddRouting();
        appBuilder.Services.AddLogging();
        appBuilder.Services.AddSingleton(this.OutboxService);
        appBuilder.Services.AddSingleton(new NotificationEmailEndpointsOptions());
        appBuilder.Services.AddNotificationEndpoints();

        var app = appBuilder.Build();
        app.UseRouting();
        app.MapEndpoints();
        app.Start();

        return app;
    }
}

public class NotificationEmailEndpointsTests : IAsyncDisposable
{
    private readonly NotificationEmailEndpointsApplication factory;
    private readonly HttpClient client;
    private readonly INotificationEmailOutboxService outboxService;

    public NotificationEmailEndpointsTests()
    {
        this.factory = new NotificationEmailEndpointsApplication();
        this.client = this.factory.CreateClient();
        this.outboxService = this.factory.OutboxService;
    }

    public async ValueTask DisposeAsync()
    {
        await this.factory.DisposeAsync();
    }

    [Fact]
    public async Task GetMessages_ShouldReturnNotificationEmails()
    {
        // Arrange
        var createdAt = DateTimeOffset.UtcNow;
        this.outboxService.GetMessagesAsync(
                EmailMessageStatus.Pending,
                "Todo created",
                "node-a",
                false,
                null,
                null,
                25,
                Arg.Any<CancellationToken>())
            .Returns(
            [
                new NotificationEmailInfo
                {
                    Id = Guid.NewGuid(),
                    Subject = "Todo created",
                    Status = EmailMessageStatus.Pending,
                    CreatedAt = createdAt,
                    IsArchived = false,
                    To = ["recipient@example.com"]
                }
            ]);

        // Act
        var response = await this.client.GetAsync("/api/_system/notifications/emails?status=Pending&subject=Todo%20created&lockedBy=node-a&isArchived=false&take=25");
        var result = await response.Content.ReadFromJsonAsync<List<NotificationEmailInfo>>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].Subject.ShouldBe("Todo created");
        await this.outboxService.Received(1).GetMessagesAsync(
            EmailMessageStatus.Pending,
            "Todo created",
            "node-a",
            false,
            null,
            null,
            25,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetMessageStats_ShouldPassArchiveFilterToService()
    {
        // Arrange
        this.outboxService.GetMessageStatsAsync(null, null, true, Arg.Any<CancellationToken>())
            .Returns(new NotificationEmailStats
            {
                Total = 3,
                Archived = 3,
                Sent = 3
            });

        // Act
        var response = await this.client.GetAsync("/api/_system/notifications/emails/stats?isArchived=true");
        var result = await response.Content.ReadFromJsonAsync<NotificationEmailStats>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Archived.ShouldBe(3);
        await this.outboxService.Received(1).GetMessageStatsAsync(null, null, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ArchiveMessage_ShouldInvokeService_WhenEmailIsTerminal()
    {
        // Arrange
        var id = Guid.NewGuid();
        this.outboxService.GetMessageAsync(id, Arg.Any<CancellationToken>())
            .Returns(new NotificationEmailInfo
            {
                Id = id,
                Status = EmailMessageStatus.Sent,
                IsArchived = false
            });

        // Act
        var response = await this.client.PostAsync($"/api/_system/notifications/emails/{id}/archive", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await this.outboxService.Received(1).ArchiveMessageAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ArchiveMessage_ShouldReturnConflict_WhenEmailIsNotTerminal()
    {
        // Arrange
        var id = Guid.NewGuid();
        this.outboxService.GetMessageAsync(id, Arg.Any<CancellationToken>())
            .Returns(new NotificationEmailInfo
            {
                Id = id,
                Status = EmailMessageStatus.Pending,
                IsArchived = false
            });

        // Act
        var response = await this.client.PostAsync($"/api/_system/notifications/emails/{id}/archive", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await this.outboxService.DidNotReceive().ArchiveMessageAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PurgeMessages_ShouldPassFiltersToService()
    {
        // Arrange
        var olderThan = DateTimeOffset.UtcNow.AddDays(-7);
        var expectedStatuses = new[] { EmailMessageStatus.Sent, EmailMessageStatus.Failed };

        // Act
        var response = await this.client.DeleteAsync(
            $"/api/_system/notifications/emails?olderThan={Uri.EscapeDataString(olderThan.ToString("O"))}&statuses=Sent&statuses=Failed&isArchived=true");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await this.outboxService.Received(1).PurgeMessagesAsync(
            Arg.Is<DateTimeOffset?>(value => value.HasValue && value.Value.ToString("O") == olderThan.ToString("O")),
            Arg.Is<IEnumerable<EmailMessageStatus>>(value => value.SequenceEqual(expectedStatuses)),
            true,
            Arg.Any<CancellationToken>());
    }
}
