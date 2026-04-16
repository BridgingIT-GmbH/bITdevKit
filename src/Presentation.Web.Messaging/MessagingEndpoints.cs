// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Messaging;

using System.Net;
using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Messaging.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Exposes operational REST endpoints for inspecting and managing persisted broker messages.
/// </summary>
/// <example>
/// <code>
/// services.AddSingleton(new MessagingEndpointsOptions());
/// services.AddMessagingEndpoints();
/// </code>
/// </example>
public class MessagingEndpoints(
    ILoggerFactory loggerFactory,
    IMessageBrokerService messageBrokerService,
    MessagingEndpointsOptions options = null) : EndpointsBase
{
    private readonly ILogger<MessagingEndpoints> logger = loggerFactory?.CreateLogger<MessagingEndpoints>() ?? NullLogger<MessagingEndpoints>.Instance;
    private readonly MessagingEndpointsOptions options = options ?? new MessagingEndpointsOptions();

    /// <summary>
    /// Maps the messaging endpoints into the current endpoint route builder.
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
            .Produces<BrokerMessageStats>()
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_System.Messaging.GetMessageStats")
            .WithSummary("Get broker message statistics")
            .WithDescription("Retrieves aggregated statistics for persisted broker messages.");

        group.MapGet(string.Empty, this.GetMessages)
            .Produces<IEnumerable<BrokerMessageInfo>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_System.Messaging.GetMessages")
            .WithSummary("List persisted broker messages")
            .WithDescription("Retrieves persisted broker messages with optional operational filters.");

        group.MapGet("{id:guid}", this.GetMessage)
            .Produces<BrokerMessageInfo>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_System.Messaging.GetMessage")
            .WithSummary("Get broker message details")
            .WithDescription("Retrieves a single persisted broker message including optional handler details.");

        group.MapGet("{id:guid}/content", this.GetMessageContent)
            .Produces<BrokerMessageContentInfo>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_System.Messaging.GetMessageContent")
            .WithSummary("Get broker message payload")
            .WithDescription("Retrieves the stored serialized payload for a persisted broker message.");

        group.MapPost("{id:guid}/retry", this.RetryMessage)
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_System.Messaging.RetryMessage")
            .WithSummary("Retry a broker message")
            .WithDescription("Resets a persisted broker message so retryable handler work can be processed again.");

        group.MapPost("{id:guid}/handlers/retry", this.RetryMessageHandler)
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_System.Messaging.RetryMessageHandler")
            .WithSummary("Retry a broker message handler")
            .WithDescription("Resets a single persisted handler entry so it can be processed again.");

        //group.MapPost("{id:guid}/lease/release", this.ReleaseLease)
        //    .Produces<string>()
        //    .Produces<string>((int)HttpStatusCode.NotFound)
        //    .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
        //    .WithName("_System.Messaging.ReleaseLease")
        //    .WithSummary("Release a broker message lease")
        //    .WithDescription("Releases the current lease for a persisted broker message.");

        group.MapPost("{id:guid}/archive", this.ArchiveMessage)
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_System.Messaging.ArchiveMessage")
            .WithSummary("Archive a broker message")
            .WithDescription("Archives a terminal persisted broker message.");

        group.MapDelete(string.Empty, this.PurgeMessages)
            .Produces<string>()
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_System.Messaging.PurgeMessages")
            .WithSummary("Purge broker messages")
            .WithDescription("Purges persisted broker messages by age and optional status filters.");

        this.IsRegistered = true;
    }

    private async Task<IResult> GetMessages([AsParameters] BrokerMessagesQueryModel request, CancellationToken cancellationToken)
    {
        this.logger.LogInformation(
            "Fetching broker messages (Status={Status}, Type={Type}, MessageId={MessageId}, LockedBy={LockedBy}, IsArchived={IsArchived}, Take={Take})",
            request.Status,
            request.Type,
            request.MessageId,
            request.LockedBy,
            request.IsArchived,
            request.Take);

        var messages = await messageBrokerService.GetMessagesAsync(
            request.Status,
            request.Type,
            request.MessageId,
            request.LockedBy,
            request.IsArchived,
            request.CreatedAfter,
            request.CreatedBefore,
            request.IncludeHandlers,
            request.Take,
            cancellationToken);

        return Results.Ok(messages);
    }

    private async Task<IResult> GetMessage(Guid id, [FromQuery] bool includeHandlers, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Fetching broker message {BrokerMessageId}", id);
        var message = await messageBrokerService.GetMessageAsync(id, includeHandlers, cancellationToken);

        return message is not null
            ? Results.Ok(message)
            : Results.NotFound($"Broker message {id} was not found.");
    }

    private async Task<IResult> GetMessageContent(Guid id, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Fetching broker message content {BrokerMessageId}", id);
        var content = await messageBrokerService.GetMessageContentAsync(id, cancellationToken);

        return content is not null
            ? Results.Ok(content)
            : Results.NotFound($"Broker message {id} was not found.");
    }

    private async Task<IResult> GetMessageStats([AsParameters] BrokerMessageStatsQueryModel request, CancellationToken cancellationToken)
    {
        this.logger.LogInformation(
            "Fetching broker message statistics (StartDate={StartDate}, EndDate={EndDate}, IsArchived={IsArchived})",
            request.StartDate,
            request.EndDate,
            request.IsArchived);

        var stats = await messageBrokerService.GetMessageStatsAsync(request.StartDate, request.EndDate, request.IsArchived, cancellationToken);

        return Results.Ok(stats);
    }

    private async Task<IResult> RetryMessage(Guid id, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Retrying broker message {BrokerMessageId}", id);
        var message = await messageBrokerService.GetMessageAsync(id, true, cancellationToken);
        if (message is null)
        {
            return Results.NotFound($"Broker message {id} was not found.");
        }

        if (!(message.Handlers?.Any(handler => handler.Status is BrokerMessageHandlerStatus.Failed or BrokerMessageHandlerStatus.DeadLettered or BrokerMessageHandlerStatus.Expired) ?? false))
        {
            return Results.Problem($"Broker message {id} does not contain retryable handler work.", statusCode: (int)HttpStatusCode.Conflict);
        }

        await messageBrokerService.RetryMessageAsync(id, cancellationToken);

        return Results.Ok($"Broker message {id} was scheduled for retry.");
    }

    private async Task<IResult> RetryMessageHandler(Guid id, [FromBody] RetryBrokerMessageHandlerModel request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.HandlerType))
        {
            return Results.Problem("HandlerType is required.", statusCode: (int)HttpStatusCode.BadRequest);
        }

        this.logger.LogInformation("Retrying broker message handler {HandlerType} for message {BrokerMessageId}", request.HandlerType, id);
        var message = await messageBrokerService.GetMessageAsync(id, true, cancellationToken);
        if (message is null)
        {
            return Results.NotFound($"Broker message {id} was not found.");
        }

        var handler = (message.Handlers ?? []).FirstOrDefault(item => string.Equals(item.HandlerType, request.HandlerType, StringComparison.Ordinal));
        if (handler is null)
        {
            return Results.NotFound($"Handler {request.HandlerType} for broker message {id} was not found.");
        }

        if (handler.Status is not (BrokerMessageHandlerStatus.Failed or BrokerMessageHandlerStatus.DeadLettered or BrokerMessageHandlerStatus.Expired))
        {
            return Results.Problem($"Handler {request.HandlerType} for broker message {id} is not in a retryable state.", statusCode: (int)HttpStatusCode.Conflict);
        }

        await messageBrokerService.RetryMessageHandlerAsync(id, request.HandlerType, cancellationToken);

        return Results.Ok($"Handler {request.HandlerType} for broker message {id} was scheduled for retry.");
    }

    //private async Task<IResult> ReleaseLease(Guid id, CancellationToken cancellationToken)
    //{
    //    this.logger.LogInformation("Releasing lease for broker message {BrokerMessageId}", id);
    //    var message = await messageBrokerService.GetMessageAsync(id, false, cancellationToken);
    //    if (message is null)
    //    {
    //        return Results.NotFound($"Broker message {id} was not found.");
    //    }

    //    await messageBrokerService.ReleaseLeaseAsync(id, cancellationToken);

    //    return Results.Ok($"Lease for broker message {id} was released.");
    //}

    private async Task<IResult> ArchiveMessage(Guid id, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Archiving broker message {BrokerMessageId}", id);
        var message = await messageBrokerService.GetMessageAsync(id, false, cancellationToken);
        if (message is null)
        {
            return Results.NotFound($"Broker message {id} was not found.");
        }

        if (message.IsArchived)
        {
            return Results.Ok($"Broker message {id} is already archived.");
        }

        if (message.Status is not (BrokerMessageStatus.Succeeded or BrokerMessageStatus.DeadLettered or BrokerMessageStatus.Expired))
        {
            return Results.Problem($"Broker message {id} is not in a terminal state and cannot be archived.", statusCode: (int)HttpStatusCode.Conflict);
        }

        await messageBrokerService.ArchiveMessageAsync(id, cancellationToken);

        return Results.Ok($"Broker message {id} was archived.");
    }

    private async Task<IResult> PurgeMessages([AsParameters] BrokerMessagesPurgeModel request, CancellationToken cancellationToken)
    {
        this.logger.LogInformation(
            "Purging broker messages (OlderThan={OlderThan}, StatusCount={StatusCount}, IsArchived={IsArchived})",
            request.OlderThan,
            request.Statuses?.Length ?? 0,
            request.IsArchived);

        await messageBrokerService.PurgeMessagesAsync(
            request.OlderThan,
            request.Statuses,
            request.IsArchived,
            cancellationToken);

        return Results.Ok("Broker messages were purged successfully.");
    }
}
