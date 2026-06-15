// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard.Pages;

using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides convenience helpers for dashboard RazorSlices.
/// </summary>
/// <example>
/// <code>
/// var contentUrl = Dashboard.Url("overview", "content");
/// var requester = Dashboard.RequiredService&lt;IRequester&gt;();
/// </code>
/// </example>
public sealed class DashboardSliceHelper(HttpContext httpContext)
{
    /// <summary>
    /// Gets the request cancellation token.
    /// </summary>
    public CancellationToken RequestAborted => httpContext.RequestAborted;

    /// <summary>
    /// Resolves an optional request service.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <returns>The service instance, or <c>null</c>.</returns>
    public T Service<T>()
    {
        return httpContext.RequestServices.GetService<T>();
    }

    /// <summary>
    /// Resolves a required request service.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <returns>The service instance.</returns>
    public T RequiredService<T>()
        where T : notnull
    {
        return httpContext.RequestServices.GetRequiredService<T>();
    }

    /// <summary>
    /// Gets an absolute URL for a page or fragment configured by a <see cref="DashboardPageSet" />.
    /// </summary>
    /// <param name="pageKey">The page key.</param>
    /// <param name="fragmentKey">The optional fragment key.</param>
    /// <returns>The absolute dashboard URL.</returns>
    public string Url(string pageKey, string fragmentKey = null)
    {
        return httpContext.RequestServices
            .GetServices<IDashboardPageProvider>()
            .OfType<DashboardPageSet>()
            .Select(pageSet => pageSet.GetUrl(pageKey, fragmentKey))
            .FirstOrDefault(url => !string.IsNullOrWhiteSpace(url));
    }

    /// <summary>
    /// Creates a stable DOM id for a dashboard content region.
    /// </summary>
    /// <param name="key">The region key.</param>
    /// <returns>The content region id.</returns>
    public string ContentId(string key)
    {
        return $"dashboard-{Normalize(key)}-content";
    }

    private static string Normalize(string value)
    {
        return string.Join(
            "-",
            (value ?? "content")
                .Split(['-', '_', '.', '/', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => part.ToLowerInvariant()));
    }
}

