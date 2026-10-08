// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Profiling.Dashboard;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using HttpResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>Maps authorized retained-operation pages and dashboard-only JSON selections.</summary>
/// <example><code>app.MapEndpoints();</code></example>
public sealed partial class DashboardEndpoints
{
    /// <summary>Builds the capability-selecting Profiling landing path under the configured dashboard prefix.</summary>
    /// <example><code>var path = DashboardEndpoints.BuildRootPath(options);</code></example>
    public static string BuildRootPath(DashboardEndpointsOptions options) => DashboardPath.Combine(options.GroupPath, "/profiling");
    /// <summary>Builds an Operations or Requests page path under the configured dashboard prefix.</summary>
    /// <example><code>var path = DashboardEndpoints.BuildOperationsPath(options, requestsOnly: true);</code></example>
    public static string BuildOperationsPath(DashboardEndpointsOptions options, bool requestsOnly = false) => DashboardPath.Combine(options.GroupPath, requestsOnly ? "/profiling/requests" : "/profiling/operations");

    private void MapOperationRoutes(RouteGroupBuilder group)
    {
        group.MapGet("/profiling", (HttpContext context) =>
        {
            var profiling = context.RequestServices.GetService<ProfilingOptions>();
            var target = profiling?.RuntimeEnabled == true ? BuildProfilingPath(options)
                : profiling?.OperationEnabled == true ? BuildOperationsPath(options) : BuildProfilingPath(options);
            return Results.Redirect(target + context.Request.QueryString);
        }).WithName("_bdk.Dashboard.ProfilingLanding");
        foreach (var (path, name) in new[] { ("/profiling/operations", "Operations"), ("/profiling/requests", "Requests") })
        {
            var requestsOnly = name == "Requests";
            group.MapGet(path, (Func<HttpContext, Task<HttpResult>>)(context => RenderOperationListAsync(context, requestsOnly, false))).WithName("_bdk.Dashboard.Profiling" + name).Produces<string>();
            group.MapGet(path + "/content", (Func<HttpContext, Task<HttpResult>>)(context => RenderOperationListAsync(context, requestsOnly, true))).WithName("_bdk.Dashboard.Profiling" + name + "Content").Produces<string>();
            group.MapGet(path + "/{id:guid}", (HttpContext context, Guid id) => RenderOperationDetailAsync(context, id, requestsOnly, false)).WithName("_bdk.Dashboard.Profiling" + name + "Detail").Produces<string>();
            group.MapGet(path + "/{id:guid}/content", (HttpContext context, Guid id) => RenderOperationDetailAsync(context, id, requestsOnly, true)).WithName("_bdk.Dashboard.Profiling" + name + "DetailContent").Produces<string>();
        }

        const string api = "/profiling/operations/api";
        group.MapGet(api + "/records", (Func<HttpContext, Task<HttpResult>>)(context => ReadOperationSelectionAsync(context, groups: false))).WithName("_bdk.Dashboard.ProfilingOperationRecords").ExcludeFromDescription();
        group.MapGet(api + "/groups", (Func<HttpContext, Task<HttpResult>>)(context => ReadOperationSelectionAsync(context, groups: true))).WithName("_bdk.Dashboard.ProfilingOperationGroups").ExcludeFromDescription();
        group.MapGet(api + "/records/{id:guid}", (HttpContext context, Guid id) => ReadOperationAsync(context, id)).WithName("_bdk.Dashboard.ProfilingOperationRecord").ExcludeFromDescription();
        group.MapGet(api + "/records/{id:guid}/runtime", (HttpContext context, Guid id) => ReadOperationRuntimeAsync(context, id)).WithName("_bdk.Dashboard.ProfilingOperationRuntime").ExcludeFromDescription();
        group.MapGet(api + "/analysis", (Func<HttpContext, Task<HttpResult>>)AnalyzeOperationsAsync).WithName("_bdk.Dashboard.ProfilingOperationAnalysis").ExcludeFromDescription();
        group.MapGet(api + "/health", (HttpContext context) => OperationQueries(context) is { } queries ? Results.Ok(queries.GetHealth()) : OperationUnavailable()).WithName("_bdk.Dashboard.ProfilingOperationHealth").ExcludeFromDescription();
    }

    private static async Task<HttpResult> RenderOperationListAsync(HttpContext context, bool requestsOnly, bool content)
    {
        var selection = OperationProfilingDashboardQuery.Read(context.Request.Query, requestsOnly);
        var model = selection.IsFailure ? new OperationProfilingDashboardModel { RequestsOnly = requestsOnly, Error = OperationProfilingViewModelBuilder.Error(selection), StatusCode = 400 }
            : await context.RequestServices.GetRequiredService<OperationProfilingViewModelBuilder>().BuildListAsync(selection.Value, requestsOnly, context.RequestAborted).ConfigureAwait(false);
        return content ? Results.RazorSlice<Pages.Operations.Content, OperationProfilingDashboardModel>(model, model.StatusCode)
            : Results.RazorSlice<Pages.Operations.Index, OperationProfilingDashboardModel>(model, model.StatusCode);
    }

    private static async Task<HttpResult> RenderOperationDetailAsync(HttpContext context, Guid id, bool requestsOnly, bool content)
    {
        var model = await context.RequestServices.GetRequiredService<OperationProfilingViewModelBuilder>().BuildDetailAsync(id, requestsOnly, context.RequestAborted).ConfigureAwait(false);
        var status = model.Error is not null ? model.StatusCode : model.Record is null ? 404 : 200;
        return content ? Results.RazorSlice<Pages.Operations.DetailContent, OperationProfilingDashboardModel>(model, status)
            : Results.RazorSlice<Pages.Operations.Detail, OperationProfilingDashboardModel>(model, status);
    }

    private static IOperationProfilingQueryService OperationQueries(HttpContext context) =>
        context.RequestServices.GetService<ProfilingOptions>()?.Enabled == true ? context.RequestServices.GetService<IOperationProfilingQueryService>() : null;
    private static HttpResult OperationUnavailable() => Results.Problem("Profiling history is unavailable.", statusCode: 503);

    private static async Task<HttpResult> ReadOperationSelectionAsync(HttpContext context, bool groups)
    {
        var queries = OperationQueries(context);
        if (queries is null) { return OperationUnavailable(); }

        var query = OperationProfilingDashboardQuery.Read(context.Request.Query);
        if (query.IsFailure) { return OperationResponse(query); }

        return groups ? OperationResponse(await queries.GroupAsync(query.Value, context.RequestAborted).ConfigureAwait(false))
            : OperationResponse(await queries.QueryAsync(query.Value, context.RequestAborted).ConfigureAwait(false));
    }

    private static async Task<HttpResult> ReadOperationAsync(HttpContext context, Guid id)
    {
        var queries = OperationQueries(context);
        if (queries is null) { return OperationUnavailable(); }

        var result = await queries.FindAsync(id, context.RequestAborted).ConfigureAwait(false);
        return result.IsSuccess && result.Value is null ? Results.NotFound(new { reason = "NotRetained", detail = "This ID is not in the provider's retained history. It may be pending, unrecorded, lost, cleared or expired.", providerScope = queries.GetHealth().ProviderScope }) : OperationResponse(result);
    }

    private static async Task<HttpResult> ReadOperationRuntimeAsync(HttpContext context, Guid id)
    {
        var queries = OperationQueries(context);
        return queries is null ? OperationUnavailable() : OperationResponse(await queries.GetRuntimeOverlayAsync(id, context.RequestAborted).ConfigureAwait(false));
    }

    private static async Task<HttpResult> AnalyzeOperationsAsync(HttpContext context)
    {
        var queries = OperationQueries(context);
        if (queries is null) { return OperationUnavailable(); }

        var baseline = OperationProfilingDashboardQuery.Read(context.Request.Query);
        if (baseline.IsFailure) { return OperationResponse(baseline); }

        var candidateText = context.Request.Query["candidate"].FirstOrDefault();
        if (candidateText is null) { return OperationResponse(await queries.AnalyzeAsync(baseline.Value, context.RequestAborted).ConfigureAwait(false)); }

        var candidateInput = context.Request.Query.Where(pair => pair.Key is not ("query" or "candidate" or "key" or "cursor")).ToDictionary(pair => pair.Key, pair => pair.Value);
        candidateInput["query"] = candidateText;
        var candidate = OperationProfilingDashboardQuery.Read(new QueryCollection(candidateInput));
        return candidate.IsFailure ? OperationResponse(candidate) : OperationResponse(await queries.CompareAsync(baseline.Value, candidate.Value, context.RequestAborted).ConfigureAwait(false));
    }

    private static HttpResult OperationResponse<T>(BridgingIT.DevKit.Common.IResult<T> result)
    {
        if (result.IsSuccess) { return Results.Ok(result.Value); }

        var status = OperationProfilingViewModelBuilder.ErrorStatus(result);
        return Results.Problem(OperationProfilingViewModelBuilder.Error(result), statusCode: status, title: "Profiling view unavailable");
    }
}
