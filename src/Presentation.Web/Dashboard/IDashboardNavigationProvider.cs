// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

using BridgingIT.DevKit.Presentation.Web.Dashboard.Pages;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Provides dashboard sidebar navigation items.
/// </summary>
/// <example>
/// <code>
/// public sealed class JobsDashboardNavigationProvider : IDashboardNavigationProvider
/// {
///     public IEnumerable&lt;DashboardSidebarItem&gt; GetItems(HttpContext httpContext) =&gt;
///         [new DashboardSidebarItem("Jobs", "cart", "/_bdk/dashboard/jobs")];
/// }
/// </code>
/// </example>
public interface IDashboardNavigationProvider
{
    /// <summary>
    /// Gets the sidebar items contributed by this provider for the current request.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <returns>The sidebar items to render.</returns>
    IEnumerable<DashboardSidebarItem> GetItems(HttpContext httpContext);
}
