// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Provides the core dashboard shell page descriptor.
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
        yield return new DashboardPage("Overview", "house", DashboardPath.Combine(options.GroupPath, "/"))
        {
            Group = "bdk",
            GroupOrder = 0,
            Order = 0,
            Description = "Overview",
            ShowOnIndex = false
        };

        yield return new DashboardPage("System", "speedometer2", DashboardPath.Combine(options.GroupPath, options.EndpointPaths.System))
        {
            Group = "bdk",
            GroupOrder = 0,
            Order = 10,
            Description = "System performance overview",
            Card = context =>
            {
                var dotNetMetrics = context.RequestServices.GetService<IDotNetMetricsSnapshotService>();
                var aspNetMetrics = context.RequestServices.GetService<IAspNetMetricsSnapshotService>();
                var runtime = dotNetMetrics?.GetSnapshot();
                var requests = aspNetMetrics?.GetSnapshot();

                return ValueTask.FromResult(new DashboardPageCard("System", "Runtime performance", runtime is null ? "-" : $"{runtime.CpuUsagePercent.ToString("N1", CultureInfo.InvariantCulture)}%")
                {
                    Detail = runtime is null
                        ? "Runtime metrics not available"
                        : $"{runtime.WorkingSetMb.ToString("N0", CultureInfo.InvariantCulture)} MB, {requests?.RequestsPerMinute.ToString("N1", CultureInfo.InvariantCulture) ?? "0"} rpm",
                    Icon = "speedometer2",
                    Url = DashboardPath.Combine(options.GroupPath, options.EndpointPaths.System),
                    Group = "bdk",
                    GroupOrder = 0,
                    Order = 10
                });
            }
        };
    }
}
