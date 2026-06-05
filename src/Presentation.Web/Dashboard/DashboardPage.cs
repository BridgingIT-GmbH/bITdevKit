// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

using Microsoft.AspNetCore.Http;

/// <summary>
/// Describes a dashboard plugin page.
/// </summary>
/// <example>
/// <code>
/// var page = new DashboardPage("Cities", "cloud-sun", "/_bdk/dashboard/weatherfiesta/cities")
/// {
///     Group = "WeatherFiesta"
/// };
/// </code>
/// </example>
public class DashboardPage(string title, string icon, string url)
{
    /// <summary>
    /// Gets or sets the sidebar and card title.
    /// </summary>
    public string Title { get; init; } = title;

    /// <summary>
    /// Gets or sets the Bootstrap icon name.
    /// </summary>
    public string Icon { get; init; } = icon;

    /// <summary>
    /// Gets or sets the absolute dashboard URL.
    /// </summary>
    public string Url { get; init; } = url;

    /// <summary>
    /// Gets or sets optional sidebar tooltip text.
    /// </summary>
    public string Description { get; init; }

    /// <summary>
    /// Gets or sets the sidebar group name.
    /// </summary>
    public string Group { get; init; } = "bdk";

    /// <summary>
    /// Gets or sets the group ordering value.
    /// </summary>
    public int GroupOrder { get; init; }

    /// <summary>
    /// Gets or sets the page ordering value inside its group.
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the page is shown in the sidebar.
    /// </summary>
    public bool ShowInSidebar { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the page contributes a dashboard index card.
    /// </summary>
    public bool ShowOnIndex { get; init; } = true;

    /// <summary>
    /// Gets or sets the optional sidebar badge provider.
    /// </summary>
    public Func<HttpContext, ValueTask<int?>> Badge { get; init; }

    /// <summary>
    /// Gets or sets the optional dashboard index card provider.
    /// </summary>
    public Func<HttpContext, ValueTask<DashboardPageCard>> Card { get; init; }
}

/// <summary>
/// Describes a compact dashboard index card for a plugin page.
/// </summary>
/// <example>
/// <code>
/// var card = new DashboardPageCard("Cities", "Known cities", "12");
/// </code>
/// </example>
public class DashboardPageCard(string title, string subtitle = null, string value = null)
{
    /// <summary>
    /// Gets or sets the card title.
    /// </summary>
    public string Title { get; init; } = title;

    /// <summary>
    /// Gets or sets the optional card subtitle.
    /// </summary>
    public string Subtitle { get; init; } = subtitle;

    /// <summary>
    /// Gets or sets the optional primary value.
    /// </summary>
    public string Value { get; init; } = value;

    /// <summary>
    /// Gets or sets optional supporting text.
    /// </summary>
    public string Detail { get; init; }

    /// <summary>
    /// Gets or sets the Bootstrap icon name.
    /// </summary>
    public string Icon { get; init; }

    /// <summary>
    /// Gets or sets the URL opened by the card.
    /// </summary>
    public string Url { get; init; }

    /// <summary>
    /// Gets or sets the card group name.
    /// </summary>
    public string Group { get; init; }

    /// <summary>
    /// Gets or sets the group ordering value.
    /// </summary>
    public int GroupOrder { get; init; }

    /// <summary>
    /// Gets or sets the card ordering value inside its group.
    /// </summary>
    public int Order { get; init; }
}
