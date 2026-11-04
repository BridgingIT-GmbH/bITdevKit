// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Messaging.Dashboard;

using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

    internal static readonly IReadOnlyList<DashboardWorkloadChart> DiagnosticsCharts =
    [
        new("Publication rate", "messages/s", [new("messaging_publish", "Attempts"), new("messaging_publish.completed", "Succeeded")]),
        new("Handler execution rate", "handler calls/s", [new("messaging_handle", "Attempts"), new("messaging_handle.completed", "Succeeded")]),
        new("Failure rates", "operations/s", [new("messaging_publish_failure", "Publication failures"), new("messaging_handle_failure", "Handler failures")]),
        new("Operation latency", "ms", [new("messaging_publish_duration", "Publication mean", true), new("messaging_handle_duration", "Handler mean", true)]),
    ];

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

        group.MapDashboardPage<Pages.Index>(
            MessagingPath + "/diagnostics",
            "_bdk.Dashboard.Messaging.Diagnostics", "Messaging Diagnostics", "Shows messaging diagnostics.");

        group.MapDashboardPage<Pages.Index>(
            MessagingPath + "/messages",
            "_bdk.Dashboard.Messaging.Messages", "Messaging Messages", "Shows filterable retained messages with bounded continuation.");

        group.MapDashboardPage<Pages.Data>(
            MessagingContentPath,
            "_bdk.Dashboard.MessagingContent",
            "Dashboard Messaging Content",
            "Shows the refreshable messaging dashboard content fragment.");

        var metricHistory = new DashboardMetricsHistory(DiagnosticsCharts.SelectMany(chart => chart.Series));
        app.ServiceProvider.GetRequiredService<IHostApplicationLifetime>().ApplicationStopped.Register(metricHistory.Dispose);
        group.MapGet(MessagingRealtimeDataPath, (HttpContext context) => Results.Json(metricHistory.Snapshot(
            context.RequestServices.GetService<IMetricsService>() is not null &&
            context.RequestServices.GetService<MetricsOptions>()?.Enabled != false)))
            .WithName("_bdk.Dashboard.Messaging.RealtimeData").WithSummary("Measured messaging throughput and latency").ExcludeFromDescription();

        if (IsAliveEnabled(app))
        {
            group.MapPost("/messaging/alive", async (HttpContext context, CancellationToken cancellationToken) =>
            {
                var broker = context.RequestServices.GetService<IMessageBroker>();
                if (broker is null)
                {
                    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
                }

                var message = new AliveMessage();
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
}
