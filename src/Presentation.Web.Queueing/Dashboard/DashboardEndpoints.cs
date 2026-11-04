// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Queueing.Dashboard;

using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

    internal static readonly IReadOnlyList<DashboardWorkloadChart> DiagnosticsCharts =
    [
        new("Enqueue rate", "messages/s", [new("queueing_enqueue", "Attempts"), new("queueing_enqueue.completed", "Succeeded")]),
        new("Handler execution rate", "handler calls/s", [new("queueing_handle", "Attempts"), new("queueing_handle.completed", "Succeeded")]),
        new("Failure rates", "operations/s", [new("queueing_enqueue_failure", "Enqueue failures"), new("queueing_handle_failure", "Handler failures")]),
        new("Operation latency", "ms", [new("queueing_enqueue_duration", "Enqueue mean", true), new("queueing_handle_duration", "Handler mean", true)]),
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
            QueueingPath,
            "_bdk.Dashboard.Queueing",
            "Dashboard Queueing",
            "Shows retained queue messages and queue subscriptions.");

        group.MapDashboardPage<Pages.Index>(
            QueueingPath + "/diagnostics",
            "_bdk.Dashboard.Queueing.Diagnostics", "Queueing Diagnostics", "Shows queueing diagnostics.");

        group.MapDashboardPage<Pages.Index>(
            QueueingPath + "/messages",
            "_bdk.Dashboard.Queueing.Messages", "Queueing Messages", "Shows filterable retained messages with bounded continuation.");

        group.MapDashboardPage<Pages.Data>(
            QueueingContentPath,
            "_bdk.Dashboard.QueueingContent",
            "Dashboard Queueing Content",
            "Shows the refreshable queueing dashboard content fragment.");

        var metricHistory = new DashboardMetricsHistory(DiagnosticsCharts.SelectMany(chart => chart.Series));
        app.ServiceProvider.GetRequiredService<IHostApplicationLifetime>().ApplicationStopped.Register(metricHistory.Dispose);
        group.MapGet(QueueingRealtimeDataPath, (HttpContext context) => Results.Json(metricHistory.Snapshot(
            context.RequestServices.GetService<IMetricsService>() is not null &&
            context.RequestServices.GetService<MetricsOptions>()?.Enabled != false)))
            .WithName("_bdk.Dashboard.Queueing.RealtimeData").WithSummary("Measured queueing throughput and latency").ExcludeFromDescription();

        if (IsAliveEnabled(app))
        {
            group.MapPost("/queueing/alive", async (HttpContext context, CancellationToken cancellationToken) =>
            {
                var broker = context.RequestServices.GetService<IQueueBroker>();
                if (broker is null)
                {
                    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
                }

                var message = new AliveQueueMessage();
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
}
