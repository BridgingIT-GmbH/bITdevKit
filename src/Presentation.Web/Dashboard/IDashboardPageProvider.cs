// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

using Microsoft.AspNetCore.Http;

/// <summary>
/// Provides dashboard plugin page descriptors.
/// </summary>
/// <example>
/// <code>
/// public sealed class WeatherDashboardPages : IDashboardPageProvider
/// {
///     public IEnumerable&lt;DashboardPage&gt; GetPages(HttpContext httpContext) =&gt; [];
/// }
/// </code>
/// </example>
public interface IDashboardPageProvider
{
    /// <summary>
    /// Gets dashboard page descriptors for the current request.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <returns>The dashboard page descriptors.</returns>
    IEnumerable<DashboardPage> GetPages(HttpContext httpContext);
}
