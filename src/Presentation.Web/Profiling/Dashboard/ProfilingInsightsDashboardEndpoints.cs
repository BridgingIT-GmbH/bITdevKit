// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by the LICENSE file.

namespace BridgingIT.DevKit.Presentation.Web.Profiling.Dashboard;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using HttpResult = Microsoft.AspNetCore.Http.IResult;

public sealed partial class DashboardEndpoints
{
    /// <summary>Builds the Insights page under the configured dashboard prefix.</summary>
    /// <example><code>var path = DashboardEndpoints.BuildInsightsPath(options);</code></example>
    public static string BuildInsightsPath(DashboardEndpointsOptions options) => DashboardPath.Combine(options.GroupPath, "/profiling/insights");

    private void MapInsightsRoutes(RouteGroupBuilder group)
    {
        group.MapGet("/profiling/insights", () => Results.RazorSlice<Pages.Insights.Index>()).WithName("_bdk.Dashboard.ProfilingInsights").Produces<string>();
        group.MapGet("/profiling/insights/data", (Func<HttpContext, Task<HttpResult>>)GetInsightsAsync).WithName("_bdk.Dashboard.ProfilingInsightsData").ExcludeFromDescription();
    }

    private static async Task<HttpResult> GetInsightsAsync(HttpContext context)
    {
        var queries = OperationQueries(context);
        if (queries is null) { return OperationUnavailable(); }

        var selection = OperationProfilingDashboardQuery.Read(context.Request.Query);
        if (selection.IsFailure) { return OperationResponse(selection); }

        var rank = context.Request.Query["rank"].FirstOrDefault() ?? "p95";
        if (rank is not ("p95" or "elapsed" or "count" or "failures")) { return Results.Problem("Choose p95, completed time, frequency or failure rate.", statusCode: 400); }

        var options = context.RequestServices.GetRequiredService<ProfilingOptions>();
        var to = selection.Value.ToUtc ?? (context.RequestServices.GetService<TimeProvider>() ?? TimeProvider.System).GetUtcNow();
        if (selection.Value.FromUtc is null && to.UtcTicks < TimeSpan.FromMinutes(15).Ticks)
        { return Results.Problem("Choose a later To UTC or an explicit From UTC.", statusCode: 400); }

        var query = selection.Value with
        {
            FromUtc = selection.Value.FromUtc ?? to.AddMinutes(-15), ToUtc = to, Outcomes = [], StartedWithin = true,
            IntervalOverlap = false, Cursor = null, MaximumAnalysisCount = Math.Min(10000, options.Queries.MaximumAnalysisRecords),
        };
        var result = await queries.BuildViewAsync<(OperationProfilingInsights Insights, OperationProfilingGroupPage Nodes)>(async (session, token) =>
        {
            var insights = await session.InsightsAsync(query, token).ConfigureAwait(false);
            if (insights.IsFailure) { return Result<(OperationProfilingInsights Insights, OperationProfilingGroupPage Nodes)>.Failure(insights); }

            var nodes = await session.GroupAsync(query with { PageSize = 1, Boundary = insights.Value.Boundary }, token).ConfigureAwait(false);
            return nodes.IsFailure ? Result<(OperationProfilingInsights Insights, OperationProfilingGroupPage Nodes)>.Failure(nodes)
                : Result<(OperationProfilingInsights Insights, OperationProfilingGroupPage Nodes)>.Success((insights.Value, nodes.Value));
        }, context.RequestAborted).ConfigureAwait(false);
        if (result.IsFailure) { return OperationResponse(result); }

        var value = result.Value.Insights;
        var ordered = value.Groups.OrderByDescending(group => rank switch
        {
            "elapsed" => group.CompletedElapsedMilliseconds,
            "count" => group.Count,
            "failures" => group.Count == 0 ? 0 : (double)group.Outcomes.GetValueOrDefault(OperationProfilingOutcome.Failed) / group.Count,
            _ => group.CompletedLatency.DurationUnavailable ? -1 : group.CompletedLatency.P95Milliseconds,
        }).ThenByDescending(group => group.Count).ThenBy(group => ProfilingKeyComparer.Canonicalize(group.Kind), StringComparer.Ordinal)
            .ThenBy(group => ProfilingKeyComparer.Canonicalize(group.Key), StringComparer.Ordinal).ThenBy(group => group.HttpMethod, StringComparer.OrdinalIgnoreCase);
        return Results.Json(new
        {
            query.FromUtc, query.ToUtc, value.Count, value.Outcomes, value.PartialCount, GroupCount = value.Groups.Count,
            CompletedCount = value.Groups.Sum(group => group.CompletedLatency.Count), Rank = rank,
            Groups = ordered.Take(query.PageSize).Select(group => new
            {
                group.Key, group.Kind, group.HttpMethod, Name = OperationProfilingViewModelBuilder.GroupName(group.Key, group.Kind),
                KindLabel = OperationProfilingViewModelBuilder.KindLabel(group.Kind), group.Count, group.Outcomes, group.CompletedLatency,
                group.CompletedElapsedMilliseconds, group.PartialCount, group.SegmentFailureCount, group.RepresentativeId,
                Segments = group.Segments.OrderByDescending(segment => segment.Unavailable ? -1 : segment.SelfPerOperation.MeanMilliseconds * segment.OperationCount)
                    .ThenBy(segment => segment.Path.ComparisonKey, StringComparer.Ordinal).Take(10).Select(segment => new
                    {
                        Path = segment.Path.Components, segment.OperationCount, segment.InvocationCount, segment.Unavailable, segment.PartialCoverage,
                        TotalMilliseconds = segment.TotalPerOperation.MeanMilliseconds * segment.OperationCount,
                        SelfMilliseconds = segment.SelfPerOperation.MeanMilliseconds * segment.OperationCount,
                        MeanSelfMilliseconds = segment.InvocationSelfMeanMilliseconds,
                    }),
            }),
            result.Value.Nodes.NodesTruncated,
            Nodes = result.Value.Nodes.Nodes.Select(node => new { Id = node.Identity.Id, Name = node.DisplayName ?? node.HostName,
                Details = $"{node.Identity.Key} · {node.HostName} · {node.ApplicationVersion}" }),
            Health = queries.GetHealth(),
        });
    }
}
