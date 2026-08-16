// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard.Pages;

/// <summary>
/// Represents dashboard layout model.
/// </summary>
public class DashboardLayoutModel
{
    /// <summary>
    /// Gets or sets the product name.
    /// </summary>
    public string ProductName { get; set; }

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// Gets or sets the hide side bar.
    /// </summary>
    public bool HideSideBar { get; set; } = false;
}

/// <summary>
/// Represents dashboard sidebar item.
/// </summary>
public class DashboardSidebarItem
{
    /// <summary>
    /// Initializes a new instance of the <c>DashboardSidebarItem</c> class.
    /// </summary>
    /// <param name="title">The title used by the operation.</param>
    /// <param name="icon">The icon used by the operation.</param>
    /// <param name="url">The url used by the operation.</param>
    public DashboardSidebarItem(string title, string icon, string url)
    {
        this.Title = title;
        this.Icon = icon;
        this.Url = url;
    }

    /// <summary>
    /// Initializes a new instance of the <c>DashboardSidebarItem</c> class.
    /// </summary>
    public DashboardSidebarItem()
    {
    }

    /// <summary>
    /// Gets or sets the order.
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// Gets or sets the group order.
    /// </summary>
    public int GroupOrder { get; init; }

    /// <summary>
    /// Gets or sets the group.
    /// </summary>
    public string Group { get; init; } = "bdk";

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title { get; init; }

    /// <summary>
    /// Gets or sets the icon.
    /// </summary>
    public string Icon { get; init; }

    /// <summary>
    /// Gets or sets the url.
    /// </summary>
    public string Url { get; init; }

    /// <summary>
    /// Gets or sets the tooltip.
    /// </summary>
    public string Tooltip { get; init; }

    /// <summary>
    /// Gets or sets the has badge.
    /// </summary>
    public bool HasBadge { get; init; }

    /// <summary>
    /// Gets or sets the badge count.
    /// </summary>
    public int? BadgeCount { get; init; }
}
