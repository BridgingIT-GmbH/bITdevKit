// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Queueing.Dashboard;

using System.Collections.Concurrent;
using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Maps the Queueing dashboard plugin page, content fragment, graph data, and operational actions.
/// </summary>
/// <example>
/// <code>
/// services.AddDashboard(options => options.WithPluginAssemblyContaining&lt;DashboardEndpoints&gt;());
/// </code>
/// </example>
public sealed class DashboardEndpoints(DashboardEndpointsOptions options) : EndpointsBase, IDashboardEndpoints
{
    internal const string QueueingPath = "/queueing";
    internal const string QueueingContentPath = "/queueing/content";
    internal const string QueueingRealtimeDataPath = "/queueing/data/realtime";

    private const int RealtimeSessionSampleLimit = 180;
    private static readonly ConcurrentDictionary<string, RealtimeGraphSession> realtimeGraphSessions = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override void Map(IEndpointRouteBuilder app)
    {
        options ??= new DashboardEndpointsOptions();

        if (!options.Enabled)
        {
            return;
        }

        var group = this.MapGroup(app, options)
            .WithTags("_bdk.Dashboard");

        group.MapDashboardPage<Pages.Index>(
            QueueingPath,
            "_bdk.Dashboard.Queueing",
            "Dashboard Queueing",
            "Shows retained queue messages and queue subscriptions.");

        group.MapDashboardPage<Pages.Data>(
            QueueingContentPath,
            "_bdk.Dashboard.QueueingContent",
            "Dashboard Queueing Content",
            "Shows the refreshable queueing dashboard content fragment.");

        group.MapGet(QueueingRealtimeDataPath, async (HttpContext context, CancellationToken cancellationToken) =>
        {
            var service = context.RequestServices.GetService<IQueueBrokerService>();
            if (service is null)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            var stats = await service.GetMessageStatsAsync(isArchived: false, cancellationToken: cancellationToken);
            var sample = new RealtimeGraphSample(
                DateTimeOffset.UtcNow,
                stats.Pending,
                stats.WaitingForHandler,
                stats.Processing,
                stats.Succeeded,
                stats.Failed,
                stats.DeadLettered,
                stats.OpenCircuits?.Count ?? 0);

            var sessionId = GetOrCreateRealtimeSessionId(context);
            var samples = AppendRealtimeSample(sessionId, sample);

            return Results.Ok(new { sessionId, samples });
        }).WithName("_bdk.Dashboard.Queueing.RealtimeData").WithSummary("Queueing realtime data").ExcludeFromDescription();

        if (IsAliveEnabled(app))
        {
            group.MapPost("/queueing/alive", async (HttpContext context, CancellationToken cancellationToken) =>
            {
                var broker = context.RequestServices.GetService<IQueueBroker>();
                if (broker is null)
                {
                    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
                }

                var message = new AliveQueueMessage("dashboard");
                await broker.Enqueue(message, cancellationToken);

                return Results.Ok(new
                {
                    messageId = message.MessageId,
                    correlationId = message.CorrelationId,
                });
            }).WithName("_bdk.Dashboard.Queueing.Alive").WithSummary("Enqueue queueing alive probe").ExcludeFromDescription();
        }

        group.MapPost("/queueing/messages/{id:guid}/retry", async (Guid id, HttpContext context, CancellationToken cancellationToken) =>
        {
            var service = context.RequestServices.GetService<IQueueBrokerService>();
            if (service is null)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            await service.RetryMessageAsync(id, cancellationToken);
            return Results.Ok();
        }).WithName("_bdk.Dashboard.Queueing.RetryMessage").WithSummary("Retry queue message").ExcludeFromDescription();

        group.MapPost("/queueing/messages/{id:guid}/archive", async (Guid id, HttpContext context, CancellationToken cancellationToken) =>
        {
            var service = context.RequestServices.GetService<IQueueBrokerService>();
            if (service is null)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            await service.ArchiveMessageAsync(id, cancellationToken);
            return Results.Ok();
        }).WithName("_bdk.Dashboard.Queueing.ArchiveMessage").WithSummary("Archive queue message").ExcludeFromDescription();

        group.MapPost("/queueing/queues/{queueName}/pause", async (string queueName, HttpContext context, CancellationToken cancellationToken) =>
        {
            var service = context.RequestServices.GetService<IQueueBrokerService>();
            if (service is null)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            await service.PauseQueueAsync(queueName, cancellationToken);
            return Results.Ok();
        }).WithName("_bdk.Dashboard.Queueing.PauseQueue").WithSummary("Pause queue").ExcludeFromDescription();

        group.MapPost("/queueing/queues/{queueName}/resume", async (string queueName, HttpContext context, CancellationToken cancellationToken) =>
        {
            var service = context.RequestServices.GetService<IQueueBrokerService>();
            if (service is null)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            await service.ResumeQueueAsync(queueName, cancellationToken);
            return Results.Ok();
        }).WithName("_bdk.Dashboard.Queueing.ResumeQueue").WithSummary("Resume queue").ExcludeFromDescription();

        group.MapPost("/queueing/types/{type}/pause", async (string type, HttpContext context, CancellationToken cancellationToken) =>
        {
            var service = context.RequestServices.GetService<IQueueBrokerService>();
            if (service is null)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            await service.PauseMessageTypeAsync(type, cancellationToken);
            return Results.Ok();
        }).WithName("_bdk.Dashboard.Queueing.PauseType").WithSummary("Pause queue message type").ExcludeFromDescription();

        group.MapPost("/queueing/types/{type}/resume", async (string type, HttpContext context, CancellationToken cancellationToken) =>
        {
            var service = context.RequestServices.GetService<IQueueBrokerService>();
            if (service is null)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            await service.ResumeMessageTypeAsync(type, cancellationToken);
            return Results.Ok();
        }).WithName("_bdk.Dashboard.Queueing.ResumeType").WithSummary("Resume queue message type").ExcludeFromDescription();

        group.MapPost("/queueing/types/{type}/circuit/reset", async (string type, HttpContext context, CancellationToken cancellationToken) =>
        {
            var service = context.RequestServices.GetService<IQueueBrokerService>();
            if (service is null)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            await service.ResetMessageTypeCircuitAsync(type, cancellationToken);
            return Results.Ok();
        }).WithName("_bdk.Dashboard.Queueing.ResetTypeCircuit").WithSummary("Reset queue message type circuit").ExcludeFromDescription();

        group.MapPost("/queueing/purge", async (HttpContext context, CancellationToken cancellationToken) =>
        {
            var service = context.RequestServices.GetService<IQueueBrokerService>();
            if (service is null)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            await service.PurgeMessagesAsync(DateTimeOffset.UtcNow.AddDays(1), isArchived: null, cancellationToken: cancellationToken);
            return Results.Ok();
        }).WithName("_bdk.Dashboard.Queueing.Purge").WithSummary("Purge queue messages").ExcludeFromDescription();
    }

    internal static string BuildQueueingPath(DashboardEndpointsOptions opts) =>
        DashboardPath.Combine(opts?.GroupPath, QueueingPath);

    internal static string BuildQueueingContentPath(DashboardEndpointsOptions opts) =>
        DashboardPath.Combine(opts?.GroupPath, QueueingContentPath);

    internal static string BuildQueueingRealtimeDataPath(DashboardEndpointsOptions opts) =>
        DashboardPath.Combine(opts?.GroupPath, QueueingRealtimeDataPath);

    internal static string BuildQueueingActionBase(DashboardEndpointsOptions opts) =>
        DashboardPath.Combine(opts?.GroupPath, QueueingPath);

    private static bool IsAliveEnabled(IEndpointRouteBuilder app) =>
        app.ServiceProvider.GetService<QueueingAliveOptions>()?.Enabled == true;

    private static string GetOrCreateRealtimeSessionId(HttpContext context)
    {
        var requestedSessionId = context.Request.Query.TryGetValue("sessionId", out var values)
            ? values.ToString()
            : null;

        return !string.IsNullOrWhiteSpace(requestedSessionId) && requestedSessionId.Length <= 128
            ? requestedSessionId
            : Guid.NewGuid().ToString("N");
    }

    private static IReadOnlyList<RealtimeGraphSample> AppendRealtimeSample(string sessionId, RealtimeGraphSample sample)
    {
        var session = realtimeGraphSessions.GetOrAdd(sessionId, _ => new RealtimeGraphSession());
        lock (session.Samples)
        {
            session.LastTouchedUtc = DateTimeOffset.UtcNow;
            session.Samples.Add(sample);
            if (session.Samples.Count > RealtimeSessionSampleLimit)
            {
                session.Samples.RemoveRange(0, session.Samples.Count - RealtimeSessionSampleLimit);
            }

            return session.Samples.ToArray();
        }
    }

    private sealed class RealtimeGraphSession
    {
        public List<RealtimeGraphSample> Samples { get; } = [];

        public DateTimeOffset LastTouchedUtc { get; set; } = DateTimeOffset.UtcNow;
    }

    private sealed record RealtimeGraphSample(
        DateTimeOffset TimestampUtc,
        int Pending,
        int WaitingForHandler,
        int Processing,
        int Succeeded,
        int Failed,
        int DeadLettered,
        int OpenCircuits);
}
