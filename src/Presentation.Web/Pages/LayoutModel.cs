// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Pages;

using System.Reflection;

/// <summary>
/// Represents layout model.
/// </summary>
public class LayoutModel
{
    /// <summary>
    /// Gets or sets the product name.
    /// </summary>
    public string ProductName { get; set; } = Assembly.GetEntryAssembly()?.GetName().Name;

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// Gets or sets the hide side bar.
    /// </summary>
    public bool HideSideBar { get; set; }
}

/// <summary>
/// Represents sidebar item.
/// </summary>
public class SidebarItem
{
    /// <summary>
    /// Initializes a new instance of the <c>SidebarItem</c> class.
    /// </summary>
    /// <param name="title">The title used by the operation.</param>
    /// <param name="icon">The icon used by the operation.</param>
    /// <param name="url">The url used by the operation.</param>
    public SidebarItem(string title, string icon, string url)
    {
        this.Title = title;
        this.Icon = icon;
        this.Url = url;
    }

    /// <summary>
    /// Initializes a new instance of the <c>SidebarItem</c> class.
    /// </summary>
    public SidebarItem()
    {
    }

    /// <summary>
    /// Gets or sets the order.
    /// </summary>
    public int Order { get; init; }

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
    /// Gets or sets the has badge.
    /// </summary>
    public bool HasBadge { get; init; }

    /// <summary>
    /// Gets or sets the badge count.
    /// </summary>
    public int? BadgeCount { get; init; }
}
