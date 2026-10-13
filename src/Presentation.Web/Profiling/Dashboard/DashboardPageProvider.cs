// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Profiling.Dashboard;

using System.Globalization;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Provides Profiling navigation and overview cards for Runtime, Operations and Requests.</summary>
/// <param name="dashboardOptions">The shared dashboard endpoint options.</param>
/// <example><code>var pages = provider.GetPages(httpContext);</code></example>
public sealed class DashboardPageProvider(DashboardEndpointsOptions dashboardOptions)
    : IDashboardPageProvider
{
    /// <inheritdoc />
    public IEnumerable<DashboardPage> GetPages(HttpContext httpContext)
    {
        if (
            httpContext.RequestServices.GetService<ProfilingOptions>()?.Enabled != true
            || dashboardOptions.DisabledPageKeys?.Contains("profiling") == true
            || (httpContext.RequestServices.GetService<IOperationProfilingQueryService>() is null && httpContext.RequestServices.GetService<IRuntimeProfilingQueryService>() is null)
        )
        {
            yield break;
        }

        yield return new DashboardPage(
            "profiling",
            "Profiling",
            "speedometer2",
            DashboardEndpoints.BuildRootPath(dashboardOptions)
        )
        {
            Group = "bdk",
            GroupOrder = 0,
            Order = 25,
            Description = "Analyze Runtime snapshots and retained operation timings",
            Card = GetCardAsync,
        };

        if (httpContext.RequestServices.GetService<IOperationProfilingQueryService>() is null) { yield break; }

        foreach (var requestsOnly in new[] { false, true })
        {
            yield return new DashboardPage("profiling." + (requestsOnly ? "requests" : "operations"),
                requestsOnly ? "Requests" : "Operations", requestsOnly ? "globe2" : "stopwatch",
                DashboardEndpoints.BuildOperationsPath(dashboardOptions, requestsOnly))
            {
                Group = "bdk", GroupOrder = 0, Order = requestsOnly ? 27 : 26, ShowInSidebar = false,
                Description = requestsOnly ? "Retained HTTP request profiles" : "Retained operation profiles",
                Card = context => GetOperationCardAsync(context, requestsOnly),
            };
        }
    }

    private static async ValueTask<DashboardPageCard> GetOperationCardAsync(HttpContext context, bool requestsOnly)
    {
        var options = context.RequestServices.GetRequiredService<DashboardEndpointsOptions>();
        var queries = context.RequestServices.GetService<IOperationProfilingQueryService>();
        var to = (context.RequestServices.GetService<TimeProvider>() ?? TimeProvider.System).GetUtcNow();
        try
        {
            if (queries is null) { return Card("Unavailable", "Profiling history unavailable"); }

            var result = await queries.GroupAsync(new()
            {
                FromUtc = to.AddMinutes(-15), ToUtc = to, Kind = requestsOnly ? OperationProfilingKind.HttpRequest.ToString() : null,
                Outcomes = Enum.GetValues<OperationProfilingOutcome>(), View = OperationProfilingView.Recent, PageSize = 1,
            }, context.RequestAborted).ConfigureAwait(false);
            if (result.IsFailure) { return Card("Unavailable", "Profiling history unavailable"); }

            var page = result.Value;
            var nodes = page.Nodes.Count.ToString("N0", CultureInfo.InvariantCulture) + (page.NodesTruncated ? "+" : "");
            return Card(page.TotalOperationCount.ToString("N0", CultureInfo.InvariantCulture),
                $"{page.TotalGroupCount.ToString("N0", CultureInfo.InvariantCulture)} {(page.TotalGroupCount == 1 ? "group" : "groups")} · {nodes} {(page.Nodes.Count == 1 && !page.NodesTruncated ? "node" : "nodes")}");
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { throw; }
        catch (Exception) { return Card("Unavailable", "Profiling history unavailable"); }

        DashboardPageCard Card(string value, string detail) => new(requestsOnly ? "Profiling - Requests" : "Profiling - Operations", "Retained executions · last 15 min", value)
        {
            Detail = detail, Icon = requestsOnly ? "globe2" : "stopwatch", Url = DashboardEndpoints.BuildOperationsPath(options, requestsOnly),
            Group = "bdk", GroupOrder = 0, Order = requestsOnly ? 27 : 26,
        };
    }

    private static async ValueTask<DashboardPageCard> GetCardAsync(HttpContext context)
    {
        var options = context.RequestServices.GetRequiredService<DashboardEndpointsOptions>();
        var control = context.RequestServices.GetService<IRuntimeProfilingControlService>();
        if (context.RequestServices.GetService<ProfilingOptions>()?.RuntimeEnabled != true || control is null)
        {
            var health = context.RequestServices.GetService<IOperationProfilingQueryService>()?.GetHealth();
            return CreateCard(options, health?.CaptureEnabled == true ? "Operations" : "History", $"Retained operation timings; local queue {health?.QueueRecords ?? 0}.");
        }

        try
        {
            var result = await control.GetStatusAsync(context.RequestAborted).ConfigureAwait(false);
            if (result.IsFailure || !result.Value.Available)
            {
                return CreateCard(options, "Unavailable", "Profiling infrastructure is unavailable.");
            }

            if (!result.Value.Enabled)
            {
                return CreateCard(options, "Disabled", "Profiling collection is disabled.");
            }

            var session = result.Value.Session;
            return session is null
                ? CreateCard(options, "Idle", "No profiling session is active.")
                : CreateCard(
                    options,
                    session.State.ToString(),
                    $"Session {session.Identity.Key}; {result.Value.Participations.Count} participating nodes"
                );
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return CreateCard(options, "Error", "Profiling status could not be loaded.");
        }
    }

    private static DashboardPageCard CreateCard(
        DashboardEndpointsOptions options,
        string value,
        string detail
    ) =>
        new("Profiling", "Runtime and Operation analysis", value)
        {
            Detail = detail,
            Icon = "speedometer2",
            Url = DashboardEndpoints.BuildRootPath(options),
            Group = "bdk",
            GroupOrder = 0,
            Order = 25,
        };
}
