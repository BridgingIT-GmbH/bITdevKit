// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Jobs.Dashboard;

using System.Collections.Concurrent;
using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Maps the Jobs dashboard plugin pages, content fragment, and inline action routes.
/// </summary>
/// <example>
/// <code>
/// services.AddDashboard(options => options.WithPluginAssemblyContaining&lt;DashboardEndpoints&gt;());
/// </code>
/// </example>
public sealed class DashboardEndpoints(DashboardEndpointsOptions options) : EndpointsBase, IDashboardEndpoints
{
    internal const string JobsPath = "/jobs";
    internal const string JobsContentPath = "/jobs/content";
    internal const string JobsDataContentPath = "/jobs/data/content";
    internal const string JobsRealtimeDataPath = "/jobs/data/realtime";
    internal const string JobsHistoryDataPath = "/jobs/data/history";

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
            JobsPath,
            "_bdk.Dashboard.Jobs",
            "Dashboard Jobs",
            "Shows registered jobs with their triggers, occurrences, and management actions.");

        group.MapDashboardPage<Pages.Content>(
            JobsContentPath,
            "_bdk.Dashboard.JobsContent",
            "Dashboard Jobs Content",
            "Shows the jobs dashboard content fragment.");

        group.MapDashboardPage<Pages.Data>(
            JobsDataContentPath,
            "_bdk.Dashboard.JobsDataContent",
            "Dashboard Jobs Data Content",
            "Shows the refreshable jobs dashboard data fragment.");

        // Graph data endpoints
        group.MapGet(JobsRealtimeDataPath, async (HttpContext ctx, CancellationToken ct) =>
        {
            var query = ctx.RequestServices.GetService<IJobSchedulerQueryService>();
            if (query is null)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            var summaryTask = query.GetDashboardSummaryAsync(cancellationToken: ct);
            var metricsTask = query.GetMetricsAsync(cancellationToken: ct);
            await Task.WhenAll(summaryTask, metricsTask);

            var summary = await summaryTask;
            var metrics = await metricsTask;

            if (summary.IsFailure)
            {
                return Results.Problem(summary.Errors.FirstOrDefault()?.Message ?? "Realtime jobs data could not be loaded.");
            }

            var completedCount = metrics.IsSuccess
                ? metrics.Value.OccurrenceCountsByStatus?.GetValueOrDefault(JobOccurrenceStatus.Completed) ?? 0
                : 0;

            var sample = new RealtimeGraphSample(
                DateTimeOffset.UtcNow,
                summary.Value.RunningOccurrenceCount,
                summary.Value.FailedOccurrenceCount,
                summary.Value.DueOccurrenceCount,
                completedCount);

            var sessionId = GetOrCreateRealtimeSessionId(ctx);
            var samples = AppendRealtimeSample(sessionId, sample);

            return Results.Ok(new
            {
                sessionId,
                samples,
            });
        }).WithName("_bdk.Dashboard.Jobs.RealtimeData").WithSummary("Jobs realtime data").ExcludeFromDescription();

        group.MapGet(JobsHistoryDataPath, async (HttpContext ctx, CancellationToken ct) =>
        {
            var query = ctx.RequestServices.GetService<IJobSchedulerQueryService>();
            if (query is null)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            var window = ctx.Request.Query.TryGetValue("window", out var values)
                ? values.ToString().ToLowerInvariant()
                : "day";

            var (fromUtc, bucketMinutes) = window switch
            {
                "week" => (DateTimeOffset.UtcNow.AddDays(-7), 360),
                "month" => (DateTimeOffset.UtcNow.AddDays(-30), 1440),
                _ => (DateTimeOffset.UtcNow.AddDays(-1), 60),
            };

            var timeline = await query.GetDashboardTimelineAsync(new JobSchedulerTimelineRequest
            {
                Mode = JobSchedulerTimelineMode.Occurrences,
                From = fromUtc,
                To = DateTimeOffset.UtcNow,
                Bucket = bucketMinutes,
            }, ct);

            if (timeline.IsFailure)
            {
                return Results.Problem(timeline.Errors.FirstOrDefault()?.Message ?? "History jobs data could not be loaded.");
            }

            var buckets = timeline.Value.Buckets
                .Select(bucket => new
                {
                    startUtc = bucket.BucketStartUtc,
                    endUtc = bucket.BucketEndUtc,
                    running = GetCount(bucket.CountsByStatus, "Running"),
                    completed = GetCount(bucket.CountsByStatus, "Completed"),
                    failed = GetCount(bucket.CountsByStatus, "Failed"),
                })
                .ToArray();

            return Results.Ok(new
            {
                window,
                fromUtc,
                toUtc = DateTimeOffset.UtcNow,
                bucketMinutes,
                buckets,
            });
        }).WithName("_bdk.Dashboard.Jobs.HistoryData").WithSummary("Jobs history data").ExcludeFromDescription();

        if (IsAliveEnabled(app))
        {
            group.MapPost("/jobs/alive", async (HttpContext ctx, CancellationToken ct) =>
            {
                var svc = ctx.RequestServices.GetService<IJobSchedulerService>();
                if (svc is null)
                {
                    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
                }

                var data = new AliveJobData
                {
                    Source = "dashboard",
                    CorrelationId = GuidGenerator.CreateSequential().ToString("N"),
                };
                var result = await svc.DispatchAsync(
                    AliveJob.JobName,
                    data,
                    new JobDispatchOptions
                    {
                        TriggerName = AliveJob.TriggerName,
                        CorrelationId = data.CorrelationId,
                    },
                    ct);

                return result.IsSuccess
                    ? Results.Ok(new
                    {
                        occurrenceId = result.Value.OccurrenceId,
                        correlationId = result.Value.CorrelationId,
                    })
                    : Results.Problem(result.Errors.FirstOrDefault()?.Message);
            }).WithName("_bdk.Dashboard.Jobs.Alive").WithSummary("Dispatch jobs alive probe").ExcludeFromDescription();
        }

        // Job action endpoints — resolved directly from in-process IJobSchedulerService
        group.MapPost("/jobs/dispatch/{jobName}", async (string jobName, HttpContext ctx, CancellationToken ct) =>
        {
            var svc = ctx.RequestServices.GetService<IJobSchedulerService>();
            if (svc is null) return Results.StatusCode(503);
            var result = await svc.DispatchAsync(jobName, cancellationToken: ct);
            return result.IsSuccess ? Results.Ok() : Results.Problem(result.Errors.FirstOrDefault()?.Message);
        }).WithName("_bdk.Dashboard.Jobs.Dispatch").WithSummary("Dispatch job").ExcludeFromDescription();

        group.MapPost("/jobs/pause/{jobName}", async (string jobName, HttpContext ctx, CancellationToken ct) =>
        {
            var svc = ctx.RequestServices.GetService<IJobSchedulerService>();
            if (svc is null) return Results.StatusCode(503);
            var result = await svc.PauseJobAsync(jobName, cancellationToken: ct);
            return result.IsSuccess ? Results.Ok() : Results.Problem(result.Errors.FirstOrDefault()?.Message);
        }).WithName("_bdk.Dashboard.Jobs.Pause").WithSummary("Pause job").ExcludeFromDescription();

        group.MapPost("/jobs/resume/{jobName}", async (string jobName, HttpContext ctx, CancellationToken ct) =>
        {
            var svc = ctx.RequestServices.GetService<IJobSchedulerService>();
            if (svc is null) return Results.StatusCode(503);
            var result = await svc.ResumeJobAsync(jobName, cancellationToken: ct);
            return result.IsSuccess ? Results.Ok() : Results.Problem(result.Errors.FirstOrDefault()?.Message);
        }).WithName("_bdk.Dashboard.Jobs.Resume").WithSummary("Resume job").ExcludeFromDescription();

        group.MapPost("/jobs/enable/{jobName}", async (string jobName, HttpContext ctx, CancellationToken ct) =>
        {
            var svc = ctx.RequestServices.GetService<IJobSchedulerService>();
            if (svc is null) return Results.StatusCode(503);
            var result = await svc.EnableJobAsync(jobName, cancellationToken: ct);
            return result.IsSuccess ? Results.Ok() : Results.Problem(result.Errors.FirstOrDefault()?.Message);
        }).WithName("_bdk.Dashboard.Jobs.Enable").WithSummary("Enable job").ExcludeFromDescription();

        group.MapPost("/jobs/disable/{jobName}", async (string jobName, HttpContext ctx, CancellationToken ct) =>
        {
            var svc = ctx.RequestServices.GetService<IJobSchedulerService>();
            if (svc is null) return Results.StatusCode(503);
            var result = await svc.DisableJobAsync(jobName, cancellationToken: ct);
            return result.IsSuccess ? Results.Ok() : Results.Problem(result.Errors.FirstOrDefault()?.Message);
        }).WithName("_bdk.Dashboard.Jobs.Disable").WithSummary("Disable job").ExcludeFromDescription();

        // Occurrence action endpoints
        group.MapPost("/jobs/occurrences/{occurrenceId:guid}/cancel", async (Guid occurrenceId, HttpContext ctx, CancellationToken ct) =>
        {
            var svc = ctx.RequestServices.GetService<IJobSchedulerService>();
            if (svc is null) return Results.StatusCode(503);
            var result = await svc.CancelOccurrenceAsync(occurrenceId, cancellationToken: ct);
            return result.IsSuccess ? Results.Ok() : Results.Problem(result.Errors.FirstOrDefault()?.Message);
        }).WithName("_bdk.Dashboard.Jobs.CancelOccurrence").WithSummary("Cancel occurrence").ExcludeFromDescription();

        group.MapPost("/jobs/occurrences/{occurrenceId:guid}/interrupt", async (Guid occurrenceId, HttpContext ctx, CancellationToken ct) =>
        {
            var svc = ctx.RequestServices.GetService<IJobSchedulerService>();
            if (svc is null) return Results.StatusCode(503);
            var result = await svc.InterruptOccurrenceAsync(occurrenceId, cancellationToken: ct);
            return result.IsSuccess ? Results.Ok() : Results.Problem(result.Errors.FirstOrDefault()?.Message);
        }).WithName("_bdk.Dashboard.Jobs.InterruptOccurrence").WithSummary("Interrupt occurrence").ExcludeFromDescription();

        group.MapPost("/jobs/occurrences/{occurrenceId:guid}/retry", async (Guid occurrenceId, HttpContext ctx, CancellationToken ct) =>
        {
            var svc = ctx.RequestServices.GetService<IJobSchedulerService>();
            if (svc is null) return Results.StatusCode(503);
            var result = await svc.RetryOccurrenceAsync(occurrenceId, cancellationToken: ct);
            return result.IsSuccess ? Results.Ok() : Results.Problem(result.Errors.FirstOrDefault()?.Message);
        }).WithName("_bdk.Dashboard.Jobs.RetryOccurrence").WithSummary("Retry occurrence").ExcludeFromDescription();
    }

    internal static string BuildJobsPath(DashboardEndpointsOptions opts) =>
        DashboardPath.Combine(opts?.GroupPath, JobsPath);

    internal static string BuildJobsContentPath(DashboardEndpointsOptions opts) =>
        DashboardPath.Combine(opts?.GroupPath, JobsContentPath);

    internal static string BuildJobsDataContentPath(DashboardEndpointsOptions opts) =>
        DashboardPath.Combine(opts?.GroupPath, JobsDataContentPath);

    internal static string BuildJobsActionBase(DashboardEndpointsOptions opts) =>
        DashboardPath.Combine(opts?.GroupPath, JobsPath);

    internal static string BuildJobsRealtimeDataPath(DashboardEndpointsOptions opts) =>
        DashboardPath.Combine(opts?.GroupPath, JobsRealtimeDataPath);

    internal static string BuildJobsHistoryDataPath(DashboardEndpointsOptions opts) =>
        DashboardPath.Combine(opts?.GroupPath, JobsHistoryDataPath);

    private static bool IsAliveEnabled(IEndpointRouteBuilder app) =>
        app.ServiceProvider.GetService<JobAliveOptions>()?.Enabled == true;

    private static long GetCount(IReadOnlyDictionary<string, long> countsByStatus, string key)
    {
        if (countsByStatus is null)
        {
            return 0;
        }

        return countsByStatus.TryGetValue(key, out var value) ? value : 0;
    }

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
        long Running,
        long Failed,
        long Due,
        long Completed);
}
