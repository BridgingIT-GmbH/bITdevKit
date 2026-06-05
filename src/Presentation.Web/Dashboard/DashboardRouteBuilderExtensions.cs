// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.AspNetCore.Routing;

using System.Net;
using System.Reflection;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using RazorSlices;

/// <summary>
/// Provides route helpers for dashboard plugin pages.
/// </summary>
/// <example>
/// <code>
/// group.MapDashboardPage&lt;Pages.Index&gt;("/metrics", "_bdk.Dashboard.Metrics", "Metrics");
/// </code>
/// </example>
public static class DashboardRouteBuilderExtensions
{
    /// <summary>
    /// Maps a typed RazorSlice dashboard page.
    /// </summary>
    public static RouteHandlerBuilder MapDashboardPage<TPage>(
        this RouteGroupBuilder group,
        string path,
        string endpointName,
        string title,
        string description = null)
        where TPage : IRazorSliceProxy
    {
        return group.MapGet(path, () => Results.RazorSlice<TPage>())
            .WithName(endpointName)
            .WithSummary(title)
            .WithDescription(description ?? $"Shows the {title} dashboard page.")
            .Produces<string>((int)HttpStatusCode.OK);
    }

    /// <summary>
    /// Maps a path-based compiled RazorSlice dashboard page from a plugin assembly.
    /// </summary>
    public static RouteHandlerBuilder MapDashboardPage(
        this RouteGroupBuilder group,
        string path,
        string razorIdentifier,
        Assembly assembly,
        string endpointName,
        string title,
        string description = null)
    {
        return group.MapGet(path, () => Results.Extensions.DashboardRazorSlice(razorIdentifier, assembly))
            .WithName(endpointName)
            .WithSummary(title)
            .WithDescription(description ?? $"Shows the {title} dashboard page.")
            .Produces<string>((int)HttpStatusCode.OK);
    }
}
