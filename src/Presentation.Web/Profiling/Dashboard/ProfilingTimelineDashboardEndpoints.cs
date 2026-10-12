// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Profiling.Dashboard;

using System.Globalization;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using HttpResult = Microsoft.AspNetCore.Http.IResult;

public sealed partial class DashboardEndpoints
{
    /// <summary>Builds the operation timeline page under the configured dashboard prefix.</summary>
    /// <example><code>var path = DashboardEndpoints.BuildTimelinePath(options);</code></example>
    public static string BuildTimelinePath(DashboardEndpointsOptions options) => DashboardPath.Combine(options.GroupPath, "/profiling/timeline");

    private void MapTimelineRoutes(RouteGroupBuilder group)
    {
        group.MapGet("/profiling/timeline", () => Results.RazorSlice<Pages.Timeline.Index>()).WithName("_bdk.Dashboard.ProfilingTimeline").Produces<string>();
        group.MapGet("/profiling/timeline/data", (Func<HttpContext, Task<HttpResult>>)GetTimelineAsync).WithName("_bdk.Dashboard.ProfilingTimelineData").ExcludeFromDescription();
        group.MapGet("/profiling/timeline/executions", (Func<HttpContext, Task<HttpResult>>)GetTimelineExecutionsAsync).WithName("_bdk.Dashboard.ProfilingTimelineExecutions").ExcludeFromDescription();
    }

    private static async Task<HttpResult> GetTimelineAsync(HttpContext context)
    {
        var queries = OperationQueries(context);
        if (queries is null) { return OperationUnavailable(); }

        var selection = ReadTimelineSelection(context);
        if (selection.IsFailure) { return OperationResponse(selection); }

        var count = 72;
        if (context.Request.Query.TryGetValue("buckets", out var input) && (!int.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out count) || count is < 12 or > 256))
        {
            return Results.Problem("Choose between 12 and 256 timeline buckets.", statusCode: 400);
        }

        var result = await queries.TimelineAsync(selection.Value, count, context.RequestAborted).ConfigureAwait(false);
        if (result.IsFailure) { return OperationResponse(result); }

        var value = result.Value;
        // The shared node identity intentionally hides persistence IDs during serialization.
        // This authorized dashboard projection needs an explicit ID for the existing node filter.
        return Results.Json(new
        {
            value.FromUtc, value.ToUtc, value.BucketWidth, value.TotalCount, value.Buckets, value.NodesTruncated,
            KindLabels = value.Buckets.Select(bucket => bucket.Kind).Distinct(ProfilingKeyComparer.Instance)
                .ToDictionary(kind => kind, OperationProfilingViewModelBuilder.KindLabel),
            Nodes = value.Nodes.Select(node => new
            {
                Id = node.Identity.Id, Name = node.DisplayName ?? node.HostName,
                Details = $"{node.Identity.Key} · {node.HostName} · {node.ApplicationVersion}",
            }),
        });
    }

    private static async Task<HttpResult> GetTimelineExecutionsAsync(HttpContext context)
    {
        var queries = OperationQueries(context);
        if (queries is null) { return OperationUnavailable(); }

        var selection = ReadTimelineSelection(context);
        if (selection.IsFailure) { return OperationResponse(selection); }

        // Cluster navigation freezes the clicked bin rather than following the live window.
        if (string.IsNullOrWhiteSpace(context.Request.Query["fromUtc"]) || string.IsNullOrWhiteSpace(context.Request.Query["toUtc"]) || string.IsNullOrWhiteSpace(selection.Value.Kind))
        {
            return Results.Problem("Execution selection requires the clicked kind and explicit UTC bucket bounds.", statusCode: 400);
        }

        var result = await queries.QueryAsync(selection.Value, context.RequestAborted).ConfigureAwait(false);
        if (result.IsFailure) { return OperationResponse(result); }

        return Results.Json(new
        {
            result.Value.TotalCount, result.Value.NextCursor, result.Value.HasMore,
            Records = result.Value.Records.Select(record => new
            {
                record.Id, record.Key, record.Kind, Name = OperationProfilingViewModelBuilder.GroupName(record.Key, record.Kind),
                record.StartedUtc, DurationMilliseconds = record.Duration.TotalMilliseconds, Outcome = record.Outcome.ToString(),
                Node = record.Node.DisplayName ?? record.Node.HostName, NodeDetails = $"{record.Node.Identity.Key} · {record.Node.ApplicationVersion}",
            }),
        });
    }

    private static BridgingIT.DevKit.Common.IResult<OperationProfilingQuery> ReadTimelineSelection(HttpContext context)
    {
        var selection = OperationProfilingDashboardQuery.Read(context.Request.Query);
        if (selection.IsFailure) { return selection; }

        var span = context.Request.Query["resolution"].FirstOrDefault()?.ToLowerInvariant() switch
        {
            "15m" => TimeSpan.FromMinutes(15),
            "30m" => TimeSpan.FromMinutes(30),
            null or "" or "hour" => TimeSpan.FromHours(1),
            "12h" => TimeSpan.FromHours(12),
            "day" => TimeSpan.FromDays(1),
            "week" => TimeSpan.FromDays(7),
            "month" => TimeSpan.FromDays(30),
            _ => TimeSpan.Zero,
        };
        if (span == TimeSpan.Zero) { return Result<OperationProfilingQuery>.Failure(new ProfilingValidationError("Choose 15 min, 30 min, Hour, 12 hours, Day, Week or Month.")); }

        var options = context.RequestServices.GetRequiredService<ProfilingOptions>();
        var clock = context.RequestServices.GetService<TimeProvider>() ?? TimeProvider.System;
        var to = selection.Value.ToUtc ?? clock.GetUtcNow();
        DateTimeOffset from;
        try { from = selection.Value.FromUtc ?? to.Subtract(span); }
        catch (ArgumentOutOfRangeException) { return Result<OperationProfilingQuery>.Failure(new ProfilingValidationError("The timeline start is outside the supported date range.")); }

        if (from >= to) { return Result<OperationProfilingQuery>.Failure(new ProfilingValidationError("From UTC must be earlier than To UTC.")); }

        return Result<OperationProfilingQuery>.Success(selection.Value with
        {
            StartedWithin = true, IntervalOverlap = false, FromUtc = from, ToUtc = to, Outcomes = [],
            View = OperationProfilingView.Recent, PageSize = Math.Min(25, options.Queries.MaximumPageSize),
            MaximumAnalysisCount = Math.Min(10000, options.Queries.MaximumAnalysisRecords),
        });
    }
}
