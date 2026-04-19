// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Notifications;

using System;
using System.Net;
using BridgingIT.DevKit.Application.Notifications;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Notifications.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Exposes operational REST endpoints for inspecting and managing notification email outbox state.
/// </summary>
/// <example>
/// <code>
/// services.AddNotificationEndpoints(options => options
///     .RequireAuthorization()
///     .GroupPath("/api/_system/notifications/emails"));
/// </code>
/// </example>
public class NotificationEmailEndpoints(
    ILoggerFactory loggerFactory,
    IServiceScopeFactory scopeFactory,
    NotificationEmailEndpointsOptions options = null) : EndpointsBase
{
    private readonly ILogger<NotificationEmailEndpoints> logger = loggerFactory?.CreateLogger<NotificationEmailEndpoints>() ?? NullLogger<NotificationEmailEndpoints>.Instance;
    private readonly IServiceScopeFactory scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly NotificationEmailEndpointsOptions options = options ?? new NotificationEmailEndpointsOptions();

    /// <summary>
    /// Maps the notification email endpoints into the current endpoint route builder.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    public override void Map(IEndpointRouteBuilder app)
    {
        if (!this.Enabled || !this.options.Enabled)
        {
            return;
        }

        var group = this.MapGroup(app, this.options);

        group.MapGet("stats", this.GetMessageStats)
            .Produces<NotificationEmailStats>()
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_System.Notifications.GetEmailStats")
            .WithSummary("Get notification email statistics")
            .WithDescription("Retrieves aggregated statistics for persisted notification emails.");

        group.MapGet(string.Empty, this.GetMessages)
            .Produces<IEnumerable<NotificationEmailInfo>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_System.Notifications.GetEmails")
            .WithSummary("List notification emails")
            .WithDescription("Retrieves persisted notification emails with optional operational filters.");

        group.MapGet("{id:guid}", this.GetMessage)
            .Produces<NotificationEmailInfo>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_System.Notifications.GetEmail")
            .WithSummary("Get notification email details")
            .WithDescription("Retrieves a single persisted notification email.");

        group.MapGet("{id:guid}/content", this.GetMessageContent)
            .Produces<NotificationEmailContentInfo>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_System.Notifications.GetEmailContent")
            .WithSummary("Get notification email body")
            .WithDescription("Retrieves the persisted body content for a notification email.");

        group.MapPost("{id:guid}/retry", this.RetryMessage)
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_System.Notifications.RetryEmail")
            .WithSummary("Retry a notification email")
            .WithDescription("Resets a failed notification email so it can be processed again by the outbox worker.");

        group.MapDelete("{id:guid}", this.DeleteMessage)
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_System.Notifications.DeleteEmail")
            .WithSummary("Delete a notification email")
            .WithDescription("Deletes a single persisted notification email from the outbox store.");

        group.MapDelete(string.Empty, this.PurgeMessages)
            .Produces<string>()
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_System.Notifications.PurgeEmails")
            .WithSummary("Purge notification emails")
            .WithDescription("Purges persisted notification emails by age and optional status filters.");

        this.IsRegistered = true;
    }

    private async Task<IResult> GetMessages([AsParameters] NotificationEmailsQueryModel request, CancellationToken cancellationToken)
    {
        this.logger.LogInformation(
            "Fetching notification emails (Status={Status}, Subject={Subject}, LockedBy={LockedBy}, Take={Take})",
            request.Status,
            request.Subject,
            request.LockedBy,
            request.Take);

        return Results.Ok(await this.WithOutboxService(
            service => service.GetMessagesAsync(
            request.Status,
            request.Subject,
            request.LockedBy,
            request.CreatedAfter,
            request.CreatedBefore,
            request.Take,
            cancellationToken)));
    }

    private async Task<IResult> GetMessage(Guid id, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Fetching notification email {NotificationEmailId}", id);
        var message = await this.WithOutboxService(service => service.GetMessageAsync(id, cancellationToken));

        return message is not null
            ? Results.Ok(message)
            : Results.NotFound($"Notification email {id} was not found.");
    }

    private async Task<IResult> GetMessageContent(Guid id, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Fetching notification email content {NotificationEmailId}", id);
        var content = await this.WithOutboxService(service => service.GetMessageContentAsync(id, cancellationToken));

        return content is not null
            ? Results.Ok(content)
            : Results.NotFound($"Notification email {id} was not found.");
    }

    private async Task<IResult> GetMessageStats([AsParameters] NotificationEmailStatsQueryModel request, CancellationToken cancellationToken)
    {
        this.logger.LogInformation(
            "Fetching notification email statistics (StartDate={StartDate}, EndDate={EndDate})",
            request.StartDate,
            request.EndDate);

        return Results.Ok(await this.WithOutboxService(service => service.GetMessageStatsAsync(request.StartDate, request.EndDate, cancellationToken)));
    }

    private async Task<IResult> RetryMessage(Guid id, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Retrying notification email {NotificationEmailId}", id);
        var message = await this.WithOutboxService(service => service.GetMessageAsync(id, cancellationToken));
        if (message is null)
        {
            return Results.NotFound($"Notification email {id} was not found.");
        }

        if (message.Status != EmailMessageStatus.Failed)
        {
            return Results.Problem($"Notification email {id} is not in a retryable state.", statusCode: (int)HttpStatusCode.Conflict);
        }

        await this.WithOutboxService(service => service.RetryMessageAsync(id, cancellationToken));
        return Results.Ok($"Notification email {id} was scheduled for retry.");
    }

    private async Task<IResult> DeleteMessage(Guid id, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Deleting notification email {NotificationEmailId}", id);
        var message = await this.WithOutboxService(service => service.GetMessageAsync(id, cancellationToken));
        if (message is null)
        {
            return Results.NotFound($"Notification email {id} was not found.");
        }

        await this.WithOutboxService(service => service.DeleteMessageAsync(id, cancellationToken));
        return Results.Ok($"Notification email {id} was deleted.");
    }

    private async Task<IResult> PurgeMessages([AsParameters] PurgeNotificationEmailsQueryModel request, CancellationToken cancellationToken)
    {
        this.logger.LogInformation(
            "Purging notification emails (OlderThan={OlderThan}, StatusCount={StatusCount})",
            request.OlderThan,
            request.Statuses?.Length ?? 0);

        await this.WithOutboxService(service => service.PurgeMessagesAsync(request.OlderThan, request.Statuses, cancellationToken));
        return Results.Ok("Notification emails were purged.");
    }

    private async Task<TResult> WithOutboxService<TResult>(Func<INotificationEmailOutboxService, Task<TResult>> action)
    {
        using var scope = this.scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<INotificationEmailOutboxService>();

        return await action(service);
    }

    private async Task WithOutboxService(Func<INotificationEmailOutboxService, Task> action)
    {
        using var scope = this.scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<INotificationEmailOutboxService>();

        await action(service);
    }
}
