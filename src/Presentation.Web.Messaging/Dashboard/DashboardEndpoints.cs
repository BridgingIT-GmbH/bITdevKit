// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Messaging.Dashboard;

using System.Collections.Concurrent;
using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Maps the Messaging dashboard plugin page, content fragment, graph data, and operational actions.
/// </summary>
/// <example>
/// <code>
/// services.AddDashboard(options => options.WithPluginAssemblyContaining&lt;DashboardEndpoints&gt;());
/// </code>
/// </example>
public sealed class DashboardEndpoints(DashboardEndpointsOptions options) : EndpointsBase, IDashboardEndpoints
{
    internal const string MessagingPath = "/messaging";
    internal const string MessagingContentPath = "/messaging/content";
    internal const string MessagingRealtimeDataPath = "/messaging/data/realtime";

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
            MessagingPath,
            "_bdk.Dashboard.Messaging",
            "Dashboard Messaging",
            "Shows persisted broker messages and message subscriptions.");

        group.MapDashboardPage<Pages.Data>(
            MessagingContentPath,
            "_bdk.Dashboard.MessagingContent",
            "Dashboard Messaging Content",
            "Shows the refreshable messaging dashboard content fragment.");

        group.MapGet(MessagingRealtimeDataPath, async (HttpContext context, CancellationToken cancellationToken) =>
        {
            var service = context.RequestServices.GetService<IMessageBrokerService>();
            if (service is null)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            var stats = await service.GetMessageStatsAsync(isArchived: false, cancellationToken: cancellationToken);
            var sample = new RealtimeGraphSample(
                DateTimeOffset.UtcNow,
                stats.Pending,
                stats.Processing,
                stats.Succeeded,
                stats.Failed,
                stats.DeadLettered);

            var sessionId = GetOrCreateRealtimeSessionId(context);
            var samples = AppendRealtimeSample(sessionId, sample);

            return Results.Ok(new { sessionId, samples });
        }).WithName("_bdk.Dashboard.Messaging.RealtimeData").WithSummary("Messaging realtime data").ExcludeFromDescription();

        if (IsAliveEnabled(app))
        {
            group.MapPost("/messaging/alive", async (HttpContext context, CancellationToken cancellationToken) =>
            {
                var broker = context.RequestServices.GetService<IMessageBroker>();
                if (broker is null)
                {
                    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
                }

                var message = new AliveMessage("dashboard");
                await broker.Publish(message, cancellationToken);

                return Results.Ok(new
                {
                    messageId = message.MessageId,
                    correlationId = message.CorrelationId,
                });
            }).WithName("_bdk.Dashboard.Messaging.Alive").WithSummary("Publish messaging alive probe").ExcludeFromDescription();
        }

        group.MapPost("/messaging/messages/{id:guid}/retry", async (Guid id, HttpContext context, CancellationToken cancellationToken) =>
        {
            var service = context.RequestServices.GetService<IMessageBrokerService>();
            if (service is null)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            await service.RetryMessageAsync(id, cancellationToken);
            return Results.Ok();
        }).WithName("_bdk.Dashboard.Messaging.RetryMessage").WithSummary("Retry broker message").ExcludeFromDescription();

        group.MapPost("/messaging/messages/{id:guid}/archive", async (Guid id, HttpContext context, CancellationToken cancellationToken) =>
        {
            var service = context.RequestServices.GetService<IMessageBrokerService>();
            if (service is null)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            await service.ArchiveMessageAsync(id, cancellationToken);
            return Results.Ok();
        }).WithName("_bdk.Dashboard.Messaging.ArchiveMessage").WithSummary("Archive broker message").ExcludeFromDescription();

        group.MapPost("/messaging/types/{type}/pause", async (string type, HttpContext context, CancellationToken cancellationToken) =>
        {
            var service = context.RequestServices.GetService<IMessageBrokerService>();
            if (service is null)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            await service.PauseMessageTypeAsync(type, cancellationToken);
            return Results.Ok();
        }).WithName("_bdk.Dashboard.Messaging.PauseType").WithSummary("Pause message type").ExcludeFromDescription();

        group.MapPost("/messaging/types/{type}/resume", async (string type, HttpContext context, CancellationToken cancellationToken) =>
        {
            var service = context.RequestServices.GetService<IMessageBrokerService>();
            if (service is null)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            await service.ResumeMessageTypeAsync(type, cancellationToken);
            return Results.Ok();
        }).WithName("_bdk.Dashboard.Messaging.ResumeType").WithSummary("Resume message type").ExcludeFromDescription();

        group.MapPost("/messaging/purge", async (HttpContext context, CancellationToken cancellationToken) =>
        {
            var service = context.RequestServices.GetService<IMessageBrokerService>();
            if (service is null)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            await service.PurgeMessagesAsync(DateTimeOffset.UtcNow.AddDays(1), isArchived: null, cancellationToken: cancellationToken);
            return Results.Ok();
        }).WithName("_bdk.Dashboard.Messaging.Purge").WithSummary("Purge broker messages").ExcludeFromDescription();
    }

    internal static string BuildMessagingPath(DashboardEndpointsOptions opts) =>
        DashboardPath.Combine(opts?.GroupPath, MessagingPath);

    internal static string BuildMessagingContentPath(DashboardEndpointsOptions opts) =>
        DashboardPath.Combine(opts?.GroupPath, MessagingContentPath);

    internal static string BuildMessagingRealtimeDataPath(DashboardEndpointsOptions opts) =>
        DashboardPath.Combine(opts?.GroupPath, MessagingRealtimeDataPath);

    internal static string BuildMessagingActionBase(DashboardEndpointsOptions opts) =>
        DashboardPath.Combine(opts?.GroupPath, MessagingPath);

    private static bool IsAliveEnabled(IEndpointRouteBuilder app) =>
        app.ServiceProvider.GetService<MessagingAliveOptions>()?.Enabled == true;

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
        int Processing,
        int Succeeded,
        int Failed,
        int DeadLettered);
}
