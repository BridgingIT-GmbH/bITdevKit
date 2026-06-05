// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Metrics.Dashboard;

using System.Globalization;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides metrics dashboard page descriptors.
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
        yield return new DashboardPage("Metrics", "people", DashboardPath.Combine(options.GroupPath, options.EndpointPaths.Metrics))
        {
            Group = "bdk",
            GroupOrder = 0,
            Order = 20,
            Card = context =>
            {
                var aspNetMetrics = context.RequestServices.GetService<IAspNetMetricsSnapshotService>();
                var dotNetMetrics = context.RequestServices.GetService<IDotNetMetricsSnapshotService>();
                var requests = aspNetMetrics?.GetSnapshot().TotalRequests;
                var cpu = dotNetMetrics?.GetSnapshot().CpuUsagePercent;

                return ValueTask.FromResult(new DashboardPageCard("Metrics", "Runtime and request metrics", requests?.ToString("N0", CultureInfo.InvariantCulture) ?? "-")
                {
                    Detail = cpu.HasValue ? $"{cpu.Value.ToString("N2", CultureInfo.InvariantCulture)}% CPU" : "Metrics not available",
                    Icon = "people",
                    Url = DashboardPath.Combine(options.GroupPath, options.EndpointPaths.Metrics),
                    Group = "bdk",
                    GroupOrder = 0,
                    Order = 20
                });
            }
        };
    }
}
