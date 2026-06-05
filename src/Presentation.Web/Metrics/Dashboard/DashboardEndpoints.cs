// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Metrics.Dashboard;

using System.Net;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using IResult = Microsoft.AspNetCore.Http.IResult;

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

        group.MapDashboardPage<Pages.Content>(
            paths.MetricsContent,
            "_bdk.Dashboard.MetricsContent",
            "Dashboard Metrics Content",
            "Shows the dashboard metrics content fragment.");
    }
}
