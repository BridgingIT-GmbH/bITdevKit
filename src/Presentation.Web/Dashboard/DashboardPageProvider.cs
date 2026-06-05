// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

using Microsoft.AspNetCore.Http;

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
        yield return new DashboardPage("Dashboard", "house", DashboardPath.Combine(options.GroupPath, "/"))
        {
            Group = "bdk",
            GroupOrder = 0,
            Order = 0,
            Description = "Dashboard overview",
            ShowOnIndex = false
        };
    }
}
