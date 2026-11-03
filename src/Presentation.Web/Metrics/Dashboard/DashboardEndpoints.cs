// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Metrics.Dashboard;

using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

/// <summary>
/// Maps the metrics dashboard plugin endpoints.
/// </summary>
/// <example>
/// <code>
/// services.AddDashboard(options => options.WithPluginAssemblyContaining&lt;DashboardEndpoints&gt;());
/// </code>
/// </example>
public class DashboardEndpoints(DashboardEndpointsOptions options) : EndpointsBase, IDashboardEndpoints
{
    /// <summary>
    /// Gets the metrics view keys in tab order. The first view is served at the metrics root path.
    /// </summary>
    /// <example>
    /// <code>
    /// var isView = DashboardEndpoints.Views.Contains("dotnet");
    /// </code>
    /// </example>
    public static IReadOnlyList<string> Views { get; } = ["app", "dotnet", "aspnet", "routes"];

    /// <summary>
    /// Builds the full metrics page path for a view.
    /// </summary>
    /// <param name="options">The dashboard options.</param>
    /// <param name="view">The view key, for example <c>dotnet</c>. Unknown keys and <c>app</c> return the metrics root path.</param>
    /// <returns>The metrics page path.</returns>
    /// <example>
    /// <code>
    /// var path = DashboardEndpoints.BuildMetricsPath(options, "routes"); // /_bdk/dashboard/metrics/routes
    /// </code>
    /// </example>
    public static string BuildMetricsPath(DashboardEndpointsOptions options, string view = "app")
    {
        var root = DashboardPath.Combine(options.GroupPath, options.EndpointPaths.Metrics);

        return view is null || view == Views[0] || !Views.Contains(view) ? root : DashboardPath.Combine(root, view);
    }

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
        var paths = options.EndpointPaths;

        group.MapDashboardPage<Pages.Index>(
            paths.Metrics,
            "_bdk.Dashboard.Metrics",
            "Dashboard Metrics",
            "Shows the dashboard metrics page.");

        foreach (var view in Views.Skip(1))
        {
            group.MapDashboardPage<Pages.Index>(
                DashboardPath.Combine(paths.Metrics, view),
                $"_bdk.Dashboard.Metrics.{view}",
                $"Dashboard Metrics {view}",
                $"Shows the dashboard metrics {view} view.");
        }

        group.MapDashboardPage<Pages.Content>(
            paths.MetricsContent,
            "_bdk.Dashboard.MetricsContent",
            "Dashboard Metrics Content",
            "Shows the dashboard metrics content fragment.");
    }
}
