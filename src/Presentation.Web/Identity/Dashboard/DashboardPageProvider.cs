// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Identity.Dashboard;

using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Provides identity dashboard page descriptors.
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
        yield return new DashboardPage("Identity", "person-badge", DashboardPath.Combine(options.GroupPath, options.EndpointPaths.Identity))
        {
            Group = "bdk",
            GroupOrder = 0,
            Order = 10,
            Card = _ => ValueTask.FromResult(new DashboardPageCard("Identity", "Current request user", httpContext.User?.Identity?.IsAuthenticated == true ? "Authenticated" : "Anonymous")
            {
                Detail = httpContext.User?.Identity?.Name,
                Icon = "person-badge",
                Url = DashboardPath.Combine(options.GroupPath, options.EndpointPaths.Identity),
                Group = "bdk",
                GroupOrder = 0,
                Order = 10
            })
        };
    }
}
