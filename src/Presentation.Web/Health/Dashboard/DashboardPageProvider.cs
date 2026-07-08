// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Health.Dashboard;

using System.Globalization;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Provides health dashboard page descriptors.
/// </summary>
/// <example>
/// <code>
/// var pages = provider.GetPages(httpContext);
/// </code>
/// </example>
public sealed class DashboardPageProvider(DashboardEndpointsOptions options) : IDashboardPageProvider
{
    /// <inheritdoc />
    public IEnumerable<DashboardPage> GetPages(HttpContext httpContext)
    {
        yield return new DashboardPage("health", "Health", "heart-pulse", DashboardEndpoints.BuildHealthPath(options))
        {
            Group = "bdk",
            GroupOrder = 0,
            Order = 30,
            Description = "Registered health check status",
            Card = GetCardAsync
        };
    }

    private static async ValueTask<DashboardPageCard> GetCardAsync(HttpContext context)
    {
        var healthCheckService = context.RequestServices.GetService<HealthCheckService>();
        if (healthCheckService is null)
        {
            return new DashboardPageCard("Health", "Registered health checks", "Unavailable")
            {
                Detail = "AddHealthChecks is not registered",
                Icon = "heart-pulse",
                Url = DashboardEndpoints.BuildHealthPath(context.RequestServices.GetRequiredService<DashboardEndpointsOptions>()),
                Group = "bdk",
                GroupOrder = 0,
                Order = 30
            };
        }

        try
        {
            var report = await healthCheckService.CheckHealthAsync(context.RequestAborted);
            var unhealthyCount = report.Entries.Count(entry => entry.Value.Status == HealthStatus.Unhealthy);
            var degradedCount = report.Entries.Count(entry => entry.Value.Status == HealthStatus.Degraded);

            return new DashboardPageCard("Health", "Registered health checks", report.Status.ToString())
            {
                Detail = $"{report.Entries.Count.ToString("N0", CultureInfo.InvariantCulture)} checks, {unhealthyCount} unhealthy, {degradedCount} degraded",
                Icon = "heart-pulse",
                Url = DashboardEndpoints.BuildHealthPath(context.RequestServices.GetRequiredService<DashboardEndpointsOptions>()),
                Group = "bdk",
                GroupOrder = 0,
                Order = 30
            };
        }
        catch (Exception ex)
        {
            return new DashboardPageCard("Health", "Registered health checks", "Error")
            {
                Detail = ex.Message,
                Icon = "heart-pulse",
                Url = DashboardEndpoints.BuildHealthPath(context.RequestServices.GetRequiredService<DashboardEndpointsOptions>()),
                Group = "bdk",
                GroupOrder = 0,
                Order = 30
            };
        }
    }
}
