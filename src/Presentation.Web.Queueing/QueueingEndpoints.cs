// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Queueing;

using System.Net;
using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Queueing.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Exposes operational REST endpoints for inspecting and managing queue broker state.
/// </summary>
/// <example>
/// <code>
/// services.AddQueueingEndpoints(options => options
///     .GroupPath("/api/_system/queueing")
///     .GroupTag("_System.Queueing"));
/// </code>
/// </example>
public class QueueingEndpoints(
    ILoggerFactory loggerFactory,
    IQueueBrokerService queueBrokerService,
    QueueingEndpointsOptions options = null) : EndpointsBase
{
    private readonly ILogger<QueueingEndpoints> logger = loggerFactory?.CreateLogger<QueueingEndpoints>() ?? NullLogger<QueueingEndpoints>.Instance;
    private readonly QueueingEndpointsOptions options = options ?? new QueueingEndpointsOptions();

    /// <summary>
    /// Maps the queueing endpoints into the current endpoint route builder.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    public override void Map(IEndpointRouteBuilder app)
    {
        if (!this.Enabled || !this.options.Enabled)
        {
            return;
        }

        var group = this.MapGroup(app, this.options);
        var messagesGroup = group.MapGroup("messages");

        messagesGroup.MapGet("stats", this.GetMessageStats)
            .Produces<QueueMessageStats>()
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_System.Queueing.GetMessageStats")
            .WithSummary("Get queue message statistics")
            .WithDescription("Retrieves aggregated statistics for retained queue messages.");

        messagesGroup.MapGet(string.Empty, this.GetMessages)
            .Produces<IEnumerable<QueueMessageInfo>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_System.Queueing.GetMessages")
            .WithSummary("List queue messages")
            .WithDescription("Retrieves retained queue messages with optional operational filters.");

        messagesGroup.MapGet("{id:guid}", this.GetMessage)
            .Produces<QueueMessageInfo>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_System.Queueing.GetMessage")
            .WithSummary("Get queue message details")
            .WithDescription("Retrieves a single retained queue message.");

        messagesGroup.MapGet("{id:guid}/content", this.GetMessageContent)
            .Produces<QueueMessageContentInfo>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_System.Queueing.GetMessageContent")
            .WithSummary("Get queue message payload")
            .WithDescription("Retrieves the stored serialized payload for a retained queue message.");

        messagesGroup.MapPost("{id:guid}/retry", this.RetryMessage)
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_System.Queueing.RetryMessage")
            .WithSummary("Retry a queue message")
            .WithDescription("Resets a retained queue message so it can be processed again.");

        //messagesGroup.MapPost("{id:guid}/lease/release", this.ReleaseLease)
        //    .Produces<string>()
        //    .Produces<string>((int)HttpStatusCode.NotFound)
        //    .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
        //    .WithName("_System.Queueing.ReleaseLease")
        //    .WithSummary("Release a queue message lease")
        //    .WithDescription("Releases the current lease for a retained queue message.");

        messagesGroup.MapPost("{id:guid}/archive", this.ArchiveMessage)
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_System.Queueing.ArchiveMessage")
            .WithSummary("Archive a queue message")
            .WithDescription("Archives a terminal retained queue message.");

        messagesGroup.MapDelete(string.Empty, this.PurgeMessages)
            .Produces<string>()
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_System.Queueing.PurgeMessages")
            .WithSummary("Purge queue messages")
            .WithDescription("Purges retained queue messages by age and optional status filters.");

        group.MapGet("stats", this.GetSummary)
            .Produces<QueueBrokerSummary>()
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_System.Queueing.GetSummary")
            .WithSummary("Get queue broker summary")
            .WithDescription("Retrieves aggregated queue broker state, including runtime capabilities and pause status.");

        group.MapGet("subscriptions", this.GetSubscriptions)
            .Produces<IEnumerable<QueueSubscriptionInfo>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_System.Queueing.GetSubscriptions")
            .WithSummary("List queue subscriptions")
            .WithDescription("Retrieves the currently active queue message type to handler registrations.");

        messagesGroup.MapGet("waiting", this.GetWaitingMessages)
            .Produces<IEnumerable<QueueMessageInfo>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_System.Queueing.GetWaitingMessages")
            .WithSummary("List waiting queue messages")
            .WithDescription("Retrieves messages that are currently waiting for a compatible handler registration.");

        group.MapPost("queues/{queueName}/pause", this.PauseQueue)
            .Produces<string>()
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_System.Queueing.PauseQueue")
            .WithSummary("Pause a queue")
            .WithDescription("Pauses queue processing for the specified logical queue name.");

        group.MapPost("queues/{queueName}/resume", this.ResumeQueue)
            .Produces<string>()
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_System.Queueing.ResumeQueue")
            .WithSummary("Resume a queue")
            .WithDescription("Resumes queue processing for the specified logical queue name.");

        group.MapPost("types/{type}/pause", this.PauseMessageType)
            .Produces<string>()
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_System.Queueing.PauseMessageType")
            .WithSummary("Pause a queue message type")
            .WithDescription("Pauses queue processing for the specified queue message type token.");

        group.MapPost("types/{type}/resume", this.ResumeMessageType)
            .Produces<string>()
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_System.Queueing.ResumeMessageType")
            .WithSummary("Resume a queue message type")
            .WithDescription("Resumes queue processing for the specified queue message type token.");

        group.MapPost("types/{type}/circuit/reset", this.ResetMessageTypeCircuit)
            .Produces<string>()
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_System.Queueing.ResetMessageTypeCircuit")
            .WithSummary("Reset a queue message type circuit")
            .WithDescription("Resets the operational circuit state for the specified queue message type token.");

        this.IsRegistered = true;
    }

    private async Task<IResult> GetMessages([AsParameters] QueueMessagesQueryModel request, CancellationToken cancellationToken)
    {
        this.logger.LogInformation(
            "Fetching queue messages (Status={Status}, Type={Type}, QueueName={QueueName}, MessageId={MessageId}, LockedBy={LockedBy}, IsArchived={IsArchived}, Take={Take})",
            request.Status,
            request.Type,
            request.QueueName,
            request.MessageId,
            request.LockedBy,
            request.IsArchived,
            request.Take);

        var messages = await queueBrokerService.GetMessagesAsync(
            request.Status,
            request.Type,
            request.QueueName,
            request.MessageId,
            request.LockedBy,
            request.IsArchived,
            request.CreatedAfter,
            request.CreatedBefore,
            request.Take,
            cancellationToken);

        return Results.Ok(messages);
    }

    private async Task<IResult> GetMessage(Guid id, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Fetching queue message {QueueMessageId}", id);
        var message = await queueBrokerService.GetMessageAsync(id, cancellationToken);

        return message is not null
            ? Results.Ok(message)
            : Results.NotFound($"Queue message {id} was not found.");
    }

    private async Task<IResult> GetMessageContent(Guid id, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Fetching queue message content {QueueMessageId}", id);
        var content = await queueBrokerService.GetMessageContentAsync(id, cancellationToken);

        return content is not null
            ? Results.Ok(content)
            : Results.NotFound($"Queue message {id} was not found.");
    }

    private async Task<IResult> GetMessageStats([AsParameters] QueueMessageStatsQueryModel request, CancellationToken cancellationToken)
    {
        this.logger.LogInformation(
            "Fetching queue message statistics (StartDate={StartDate}, EndDate={EndDate}, IsArchived={IsArchived})",
            request.StartDate,
            request.EndDate,
            request.IsArchived);

        var stats = await queueBrokerService.GetMessageStatsAsync(request.StartDate, request.EndDate, request.IsArchived, cancellationToken);
        return Results.Ok(stats);
    }

    private async Task<IResult> GetSummary(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Fetching queue broker summary");
        return Results.Ok(await queueBrokerService.GetSummaryAsync(cancellationToken));
    }

    private async Task<IResult> GetSubscriptions(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Fetching queue subscriptions");
        return Results.Ok(await queueBrokerService.GetSubscriptionsAsync(cancellationToken));
    }

    private async Task<IResult> GetWaitingMessages([FromQuery] int? take, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Fetching waiting queue messages (Take={Take})", take);
        return Results.Ok(await queueBrokerService.GetWaitingMessagesAsync(take, cancellationToken));
    }

    private async Task<IResult> RetryMessage(Guid id, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Retrying queue message {QueueMessageId}", id);
        var message = await queueBrokerService.GetMessageAsync(id, cancellationToken);
        if (message is null)
        {
            return Results.NotFound($"Queue message {id} was not found.");
        }

        if (message.Status is not (QueueMessageStatus.Failed or QueueMessageStatus.DeadLettered or QueueMessageStatus.Expired or QueueMessageStatus.WaitingForHandler))
        {
            return Results.Problem($"Queue message {id} is not in a retryable state.", statusCode: (int)HttpStatusCode.Conflict);
        }

        await queueBrokerService.RetryMessageAsync(id, cancellationToken);
        return Results.Ok($"Queue message {id} was scheduled for retry.");
    }

    //private async Task<IResult> ReleaseLease(Guid id, CancellationToken cancellationToken)
    //{
    //    this.logger.LogInformation("Releasing lease for queue message {QueueMessageId}", id);
    //    var message = await queueBrokerService.GetMessageAsync(id, cancellationToken);
    //    if (message is null)
    //    {
    //        return Results.NotFound($"Queue message {id} was not found.");
    //    }

    //    await queueBrokerService.ReleaseLeaseAsync(id, cancellationToken);
    //    return Results.Ok($"Lease for queue message {id} was released.");
    //}

    private async Task<IResult> ArchiveMessage(Guid id, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Archiving queue message {QueueMessageId}", id);
        var message = await queueBrokerService.GetMessageAsync(id, cancellationToken);
        if (message is null)
        {
            return Results.NotFound($"Queue message {id} was not found.");
        }

        if (message.IsArchived)
        {
            return Results.Ok($"Queue message {id} is already archived.");
        }

        if (message.Status is not (QueueMessageStatus.Succeeded or QueueMessageStatus.DeadLettered or QueueMessageStatus.Expired))
        {
            return Results.Problem($"Queue message {id} is not in a terminal state and cannot be archived.", statusCode: (int)HttpStatusCode.Conflict);
        }

        await queueBrokerService.ArchiveMessageAsync(id, cancellationToken);
        return Results.Ok($"Queue message {id} was archived.");
    }

    private async Task<IResult> PurgeMessages([AsParameters] QueueMessagesPurgeModel request, CancellationToken cancellationToken)
    {
        this.logger.LogInformation(
            "Purging queue messages (OlderThan={OlderThan}, StatusCount={StatusCount}, IsArchived={IsArchived})",
            request.OlderThan,
            request.Statuses?.Length ?? 0,
            request.IsArchived);

        await queueBrokerService.PurgeMessagesAsync(
            request.OlderThan,
            request.Statuses,
            request.IsArchived,
            cancellationToken);

        return Results.Ok("Queue messages were purged successfully.");
    }

    private async Task<IResult> PauseQueue(string queueName, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Pausing queue {QueueName}", queueName);
        await queueBrokerService.PauseQueueAsync(queueName, cancellationToken);

        return Results.Ok($"Queue {queueName} paused successfully.");
    }

    private async Task<IResult> ResumeQueue(string queueName, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Resuming queue {QueueName}", queueName);
        await queueBrokerService.ResumeQueueAsync(queueName, cancellationToken);

        return Results.Ok($"Queue {queueName} resumed successfully.");
    }

    private async Task<IResult> PauseMessageType(string type, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Pausing queue message type {QueueMessageType}", type);
        await queueBrokerService.PauseMessageTypeAsync(type, cancellationToken);

        return Results.Ok($"Queue message type {type} paused successfully.");
    }

    private async Task<IResult> ResumeMessageType(string type, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Resuming queue message type {QueueMessageType}", type);
        await queueBrokerService.ResumeMessageTypeAsync(type, cancellationToken);

        return Results.Ok($"Queue message type {type} resumed successfully.");
    }

    private async Task<IResult> ResetMessageTypeCircuit(string type, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Resetting queue message type circuit {QueueMessageType}", type);
        await queueBrokerService.ResetMessageTypeCircuitAsync(type, cancellationToken);

        return Results.Ok($"Queue message type circuit {type} reset successfully.");
    }
}
