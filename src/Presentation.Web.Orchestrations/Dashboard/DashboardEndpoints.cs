// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Orchestrations.Dashboard;

using System.Collections.Concurrent;
using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Maps the Orchestrations dashboard plugin page, fragments, graph data, and operational actions.
/// </summary>
/// <example>
/// <code>
/// services.AddDashboard(options => options.WithPluginAssemblyContaining&lt;DashboardEndpoints&gt;());
/// </code>
/// </example>
public sealed class DashboardEndpoints(DashboardEndpointsOptions options) : EndpointsBase, IDashboardEndpoints
{
    internal const string OrchestrationsPath = "/orchestrations";
    internal const string OrchestrationsContentPath = "/orchestrations/content";
    internal const string OrchestrationsRealtimeDataPath = "/orchestrations/data/realtime";
    internal const string OrchestrationsHistoryDataPath = "/orchestrations/data/history";

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
            OrchestrationsPath,
            "_bdk.Dashboard.Orchestrations",
            "Dashboard Orchestrations",
            "Shows persisted orchestration instances and operational controls.");

        group.MapDashboardPage<Pages.Data>(
            OrchestrationsContentPath,
            "_bdk.Dashboard.OrchestrationsContent",
            "Dashboard Orchestrations Content",
            "Shows the refreshable orchestration dashboard content fragment.");

        group.MapGet(OrchestrationsRealtimeDataPath, async (HttpContext context, CancellationToken cancellationToken) =>
        {
            var query = context.RequestServices.GetService<IOrchestrationQueryService>();
            if (query is null)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            var result = await query.GetMetricsAsync(cancellationToken: cancellationToken);
            if (result.IsFailure)
            {
                return Results.Problem(result.Errors.FirstOrDefault()?.Message ?? "Realtime orchestration data could not be loaded.");
            }

            var metrics = result.Value;
            var sample = new RealtimeGraphSample(
                DateTimeOffset.UtcNow,
                metrics.RunningCount,
                metrics.WaitingCount,
                metrics.PausedCount,
                metrics.CompletedCount,
                metrics.FailedCount,
                metrics.CancelledCount + metrics.TerminatedCount);

            var sessionId = GetOrCreateRealtimeSessionId(context);
            var samples = AppendRealtimeSample(sessionId, sample);

            return Results.Ok(new { sessionId, samples });
        }).WithName("_bdk.Dashboard.Orchestrations.RealtimeData").WithSummary("Orchestrations realtime data").ExcludeFromDescription();

        group.MapGet(OrchestrationsHistoryDataPath, async (HttpContext context, CancellationToken cancellationToken) =>
        {
            var query = context.RequestServices.GetService<IOrchestrationQueryService>();
            if (query is null)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            var window = context.Request.Query.TryGetValue("window", out var values)
                ? values.ToString().ToLowerInvariant()
                : "day";

            var (fromUtc, bucket) = window switch
            {
                "week" => (DateTimeOffset.UtcNow.AddDays(-7), TimeSpan.FromHours(6)),
                "month" => (DateTimeOffset.UtcNow.AddDays(-30), TimeSpan.FromDays(1)),
                _ => (DateTimeOffset.UtcNow.AddDays(-1), TimeSpan.FromHours(1)),
            };

            var result = await query.QueryAsync(new OrchestrationQueryRequest
            {
                StartedFrom = fromUtc,
                StartedTo = DateTimeOffset.UtcNow,
                Take = 5000,
                SortBy = "StartedUtc",
                SortDescending = false
            }, cancellationToken);

            if (result.IsFailure)
            {
                return Results.Problem(result.Errors.FirstOrDefault()?.Message ?? "History orchestration data could not be loaded.");
            }

            var buckets = CreateHistoryBuckets(result.Value, fromUtc, DateTimeOffset.UtcNow, bucket)
                .Select(item => new
                {
                    startUtc = item.StartUtc,
                    endUtc = item.EndUtc,
                    running = GetCount(item.CountsByStatus, "Running"),
                    waiting = GetCount(item.CountsByStatus, "Waiting"),
                    completed = GetCount(item.CountsByStatus, "Completed"),
                    failed = GetCount(item.CountsByStatus, "Failed"),
                    problem = GetCount(item.CountsByStatus, "Cancelled") + GetCount(item.CountsByStatus, "Terminated"),
                })
                .ToArray();

            return Results.Ok(new
            {
                window,
                fromUtc,
                toUtc = DateTimeOffset.UtcNow,
                bucketMinutes = (int)bucket.TotalMinutes,
                buckets,
            });
        }).WithName("_bdk.Dashboard.Orchestrations.HistoryData").WithSummary("Orchestrations history data").ExcludeFromDescription();

        if (IsAliveEnabled(app))
        {
            group.MapPost("/orchestrations/alive", async (HttpContext context, CancellationToken cancellationToken) =>
            {
                var service = context.RequestServices.GetService<IOrchestrationService>();
                if (service is null)
                {
                    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
                }

                var data = new AliveOrchestrationData
                {
                    Source = "dashboard",
                    CorrelationId = GuidGenerator.CreateSequential().ToString("N"),
                };
                var result = await service.DispatchAsync<AliveOrchestration, AliveOrchestrationData>(data, cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(new { instanceId = result.Value, correlationId = data.CorrelationId })
                    : Results.Problem(result.Errors.FirstOrDefault()?.Message);
            }).WithName("_bdk.Dashboard.Orchestrations.Alive").WithSummary("Dispatch orchestration alive probe").ExcludeFromDescription();
        }

        group.MapPost("/orchestrations/{instanceId:guid}/pause", async (Guid instanceId, HttpContext context, CancellationToken cancellationToken) =>
        {
            var service = context.RequestServices.GetService<IOrchestrationService>();
            if (service is null) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            var result = await service.PauseAsync(instanceId, "Paused from the dashboard.", cancellationToken);
            return result.IsSuccess ? Results.Ok() : Results.Problem(result.Errors.FirstOrDefault()?.Message);
        }).WithName("_bdk.Dashboard.Orchestrations.Pause").WithSummary("Pause orchestration").ExcludeFromDescription();

        group.MapPost("/orchestrations/{instanceId:guid}/resume", async (Guid instanceId, HttpContext context, CancellationToken cancellationToken) =>
        {
            var service = context.RequestServices.GetService<IOrchestrationService>();
            if (service is null) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            var result = await service.ResumeAsync(instanceId, cancellationToken);
            return result.IsSuccess ? Results.Ok() : Results.Problem(result.Errors.FirstOrDefault()?.Message);
        }).WithName("_bdk.Dashboard.Orchestrations.Resume").WithSummary("Resume orchestration").ExcludeFromDescription();

        group.MapPost("/orchestrations/{instanceId:guid}/cancel", async (Guid instanceId, HttpContext context, CancellationToken cancellationToken) =>
        {
            var service = context.RequestServices.GetService<IOrchestrationService>();
            if (service is null) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            var result = await service.CancelAsync(instanceId, "Cancelled from the dashboard.", cancellationToken);
            return result.IsSuccess ? Results.Ok() : Results.Problem(result.Errors.FirstOrDefault()?.Message);
        }).WithName("_bdk.Dashboard.Orchestrations.Cancel").WithSummary("Cancel orchestration").ExcludeFromDescription();

        group.MapPost("/orchestrations/{instanceId:guid}/terminate", async (Guid instanceId, HttpContext context, CancellationToken cancellationToken) =>
        {
            var service = context.RequestServices.GetService<IOrchestrationService>();
            if (service is null) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            var result = await service.TerminateAsync(instanceId, "Terminated from the dashboard.", cancellationToken);
            return result.IsSuccess ? Results.Ok() : Results.Problem(result.Errors.FirstOrDefault()?.Message);
        }).WithName("_bdk.Dashboard.Orchestrations.Terminate").WithSummary("Terminate orchestration").ExcludeFromDescription();

        group.MapPost("/orchestrations/{instanceId:guid}/archive", async (Guid instanceId, HttpContext context, CancellationToken cancellationToken) =>
        {
            var service = context.RequestServices.GetService<IOrchestrationAdministrationService>();
            if (service is null) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            var result = await service.ArchiveAsync(instanceId, cancellationToken);
            return result.IsSuccess ? Results.Ok() : Results.Problem(result.Errors.FirstOrDefault()?.Message);
        }).WithName("_bdk.Dashboard.Orchestrations.Archive").WithSummary("Archive orchestration").ExcludeFromDescription();

        group.MapPost("/orchestrations/{instanceId:guid}/repair/release-lease", async (Guid instanceId, HttpContext context, CancellationToken cancellationToken) =>
        {
            var service = context.RequestServices.GetService<IOrchestrationAdministrationService>();
            if (service is null) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            var result = await service.ReleaseLeaseAsync(instanceId, cancellationToken);
            return result.IsSuccess ? Results.Ok() : Results.Problem(result.Errors.FirstOrDefault()?.Message);
        }).WithName("_bdk.Dashboard.Orchestrations.ReleaseLease").WithSummary("Release orchestration lease").ExcludeFromDescription();

        group.MapPost("/orchestrations/{instanceId:guid}/repair/requeue-timers", async (Guid instanceId, HttpContext context, CancellationToken cancellationToken) =>
        {
            var service = context.RequestServices.GetService<IOrchestrationAdministrationService>();
            if (service is null) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            var result = await service.RequeueTimersAsync(instanceId, cancellationToken);
            return result.IsSuccess ? Results.Ok() : Results.Problem(result.Errors.FirstOrDefault()?.Message);
        }).WithName("_bdk.Dashboard.Orchestrations.RequeueTimers").WithSummary("Requeue orchestration timers").ExcludeFromDescription();

        group.MapPost("/orchestrations/purge", async (HttpContext context, CancellationToken cancellationToken) =>
        {
            var service = context.RequestServices.GetService<IOrchestrationAdministrationService>();
            if (service is null) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

            var result = await service.PurgeAsync(new OrchestrationPurgeRequest
            {
                OlderThan = DateTimeOffset.UtcNow.AddDays(1),
            }, cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : Results.Problem(result.Errors.FirstOrDefault()?.Message);
        }).WithName("_bdk.Dashboard.Orchestrations.Purge").WithSummary("Purge orchestration data").ExcludeFromDescription();
    }

    internal static string BuildOrchestrationsPath(DashboardEndpointsOptions opts) =>
        DashboardPath.Combine(opts?.GroupPath, OrchestrationsPath);

    internal static string BuildOrchestrationsContentPath(DashboardEndpointsOptions opts) =>
        DashboardPath.Combine(opts?.GroupPath, OrchestrationsContentPath);

    internal static string BuildOrchestrationsRealtimeDataPath(DashboardEndpointsOptions opts) =>
        DashboardPath.Combine(opts?.GroupPath, OrchestrationsRealtimeDataPath);

    internal static string BuildOrchestrationsHistoryDataPath(DashboardEndpointsOptions opts) =>
        DashboardPath.Combine(opts?.GroupPath, OrchestrationsHistoryDataPath);

    internal static string BuildOrchestrationsActionBase(DashboardEndpointsOptions opts) =>
        DashboardPath.Combine(opts?.GroupPath, OrchestrationsPath);

    private static bool IsAliveEnabled(IEndpointRouteBuilder app) =>
        app.ServiceProvider.GetService<OrchestrationAliveOptions>()?.Enabled == true;

    private static long GetCount(IReadOnlyDictionary<string, long> countsByStatus, string status) =>
        countsByStatus is not null && countsByStatus.TryGetValue(status, out var value) ? value : 0;

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

    private static IReadOnlyList<HistoryBucket> CreateHistoryBuckets(
        IEnumerable<OrchestrationInstanceModel> instances,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        TimeSpan bucketSize)
    {
        var buckets = new List<HistoryBucket>();
        for (var cursor = fromUtc; cursor < toUtc; cursor = cursor.Add(bucketSize))
        {
            var end = cursor.Add(bucketSize);
            buckets.Add(new HistoryBucket(cursor, end));
        }

        foreach (var instance in instances ?? [])
        {
            var bucket = buckets.FirstOrDefault(item => instance.StartedUtc >= item.StartUtc && instance.StartedUtc < item.EndUtc);
            if (bucket is null)
            {
                continue;
            }

            var status = string.IsNullOrWhiteSpace(instance.Status) ? "Unknown" : instance.Status;
            bucket.CountsByStatus[status] = bucket.CountsByStatus.TryGetValue(status, out var count) ? count + 1 : 1;
        }

        return buckets;
    }

    private sealed class RealtimeGraphSession
    {
        public List<RealtimeGraphSample> Samples { get; } = [];

        public DateTimeOffset LastTouchedUtc { get; set; } = DateTimeOffset.UtcNow;
    }

    private sealed record RealtimeGraphSample(
        DateTimeOffset TimestampUtc,
        long Running,
        long Waiting,
        long Paused,
        long Completed,
        long Failed,
        long Problem);

    private sealed record HistoryBucket(DateTimeOffset StartUtc, DateTimeOffset EndUtc)
    {
        public Dictionary<string, long> CountsByStatus { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
