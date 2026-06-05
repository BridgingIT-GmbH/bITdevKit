// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard.Pages;

public class DashboardLayoutModel
{
    public string ProductName { get; set; }

    public string Title { get; set; } = "";

    public bool HideSideBar { get; set; } = false;
}

public class DashboardSidebarItem
{
    public DashboardSidebarItem(string title, string icon, string url)
    {
        this.Title = title;
        this.Icon = icon;
        this.Url = url;
    }

    public DashboardSidebarItem()
    {
    }

    public int Order { get; init; }

    public int GroupOrder { get; init; }

    public string Group { get; init; } = "bdk";

    public string Title { get; init; }

    public string Icon { get; init; }

    public string Url { get; init; }

    public string Tooltip { get; init; }

    public bool HasBadge { get; init; }

    public int? BadgeCount { get; init; }
}
