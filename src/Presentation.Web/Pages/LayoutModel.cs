// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Pages;

using System.Reflection;

public class LayoutModel
{
    public string ProductName { get; set; } = Assembly.GetEntryAssembly()?.GetName().Name;

    public string Title { get; set; } = "";

    public bool HideSideBar { get; set; }
}

public class SidebarItem
{
    public SidebarItem(string title, string icon, string url)
    {
        this.Title = title;
        this.Icon = icon;
        this.Url = url;
    }

    public SidebarItem()
    {
    }

    public int Order { get; init; }

    public string Title { get; init; }

    public string Icon { get; init; }

    public string Url { get; init; }

    public bool HasBadge { get; init; }

    public int? BadgeCount { get; init; }
}