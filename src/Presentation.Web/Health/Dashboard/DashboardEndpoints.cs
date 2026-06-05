// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Health.Dashboard;

using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

/// <summary>
/// Maps the health dashboard plugin endpoints.
/// </summary>
/// <example>
/// <code>
/// services.AddDashboard(options => options.WithPluginAssemblyContaining&lt;DashboardEndpoints&gt;());
/// </code>
/// </example>
public sealed class DashboardEndpoints(DashboardEndpointsOptions options) : EndpointsBase, IDashboardEndpoints
{
    private const string HealthPath = "/health";
    private const string HealthContentPath = "/health/content";

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
            HealthPath,
            "_bdk.Dashboard.Health",
            "Dashboard Health",
            "Shows registered ASP.NET Core health checks.");

        group.MapDashboardPage<Pages.Content>(
            HealthContentPath,
            "_bdk.Dashboard.HealthContent",
            "Dashboard Health Content",
            "Shows the dashboard health content fragment.");
    }

    internal static string BuildHealthPath(DashboardEndpointsOptions options)
    {
        return DashboardPath.Combine(options?.GroupPath, HealthPath);
    }

    internal static string BuildHealthContentPath(DashboardEndpointsOptions options)
    {
        return DashboardPath.Combine(options?.GroupPath, HealthContentPath);
    }
}
