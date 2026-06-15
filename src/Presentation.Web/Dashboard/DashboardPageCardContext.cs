// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

using Microsoft.AspNetCore.Http;

/// <summary>
/// Provides context and convenience methods for dashboard page card providers.
/// </summary>
/// <example>
/// <code>
/// return ValueTask.FromResult(card.Value("12", "active cities"));
/// </code>
/// </example>
public sealed class DashboardPageCardContext(
    HttpContext httpContext,
    DashboardPageDefinition page,
    string url)
{
    /// <summary>
    /// Gets the current HTTP context.
    /// </summary>
    public HttpContext HttpContext { get; } = httpContext;

    /// <summary>
    /// Gets the configured page definition.
    /// </summary>
    public DashboardPageDefinition Page { get; } = page;

    /// <summary>
    /// Creates a normal dashboard card using the page metadata.
    /// </summary>
    /// <param name="value">The primary value.</param>
    /// <param name="detail">The supporting detail text.</param>
    /// <param name="subtitle">The optional subtitle.</param>
    /// <returns>The dashboard card.</returns>
    public DashboardPageCard Value(string value, string detail = null, string subtitle = null)
    {
        return this.Create(value, detail, subtitle);
    }

    /// <summary>
    /// Creates an unavailable dashboard card using the page metadata.
    /// </summary>
    /// <param name="detail">The supporting detail text.</param>
    /// <returns>The dashboard card.</returns>
    public DashboardPageCard Unavailable(string detail)
    {
        return this.Create("-", detail);
    }

    /// <summary>
    /// Creates an error dashboard card using the page metadata.
    /// </summary>
    /// <param name="detail">The supporting detail text.</param>
    /// <returns>The dashboard card.</returns>
    public DashboardPageCard Error(string detail)
    {
        return this.Create("Error", detail);
    }

    private DashboardPageCard Create(string value, string detail, string subtitle = null)
    {
        return new DashboardPageCard(this.Page.Title, subtitle ?? this.Page.Group, value)
        {
            Detail = detail,
            Icon = this.Page.Icon,
            Url = url,
            Group = this.Page.Group,
            GroupOrder = this.Page.GroupOrder,
            Order = this.Page.Order
        };
    }
}

