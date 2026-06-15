// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RazorSlices;

/// <summary>
/// Describes a page configured through <see cref="DashboardPageSet" />.
/// </summary>
/// <example>
/// <code>
/// var definition = new DashboardPageDefinition("overview", "/app/core");
/// </code>
/// </example>
public sealed class DashboardPageDefinition(string key, string path)
{
    /// <summary>
    /// Gets the stable page key.
    /// </summary>
    public string Key { get; } = key;

    /// <summary>
    /// Gets the page route path below the dashboard group path.
    /// </summary>
    public string Path { get; } = path;

    /// <summary>
    /// Gets or sets the page title.
    /// </summary>
    public string Title { get; set; } = key;

    /// <summary>
    /// Gets or sets the Bootstrap icon name.
    /// </summary>
    public string Icon { get; set; } = "window";

    /// <summary>
    /// Gets or sets the optional page description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets optional sidebar tooltip text.
    /// </summary>
    public string Tooltip { get; set; }

    /// <summary>
    /// Gets or sets the sidebar and card group name.
    /// </summary>
    public string Group { get; set; } = "bdk";

    /// <summary>
    /// Gets or sets the group order.
    /// </summary>
    public int GroupOrder { get; set; }

    /// <summary>
    /// Gets or sets the page order inside its group.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets whether the page appears in the sidebar.
    /// </summary>
    public bool ShowInSidebar { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the page appears on the dashboard index.
    /// </summary>
    public bool ShowOnIndex { get; set; } = true;

    /// <summary>
    /// Gets or sets the optional sidebar badge provider.
    /// </summary>
    public Func<HttpContext, ValueTask<int?>> Badge { get; set; }

    /// <summary>
    /// Gets or sets the optional dashboard index card provider.
    /// </summary>
    public Func<DashboardPageCardContext, ValueTask<DashboardPageCard>> Card { get; set; }

    internal DashboardPageRouteDefinition Route { get; set; }

    internal List<DashboardPageRouteDefinition> Fragments { get; } = [];

    internal List<DashboardPageActionDefinition> Actions { get; } = [];
}

internal sealed class DashboardPageRouteDefinition
{
    private DashboardPageRouteDefinition(
        string key,
        string path,
        string endpointName,
        string title,
        string description,
        Action<RouteGroupBuilder, DashboardPageDefinition, DashboardPageRouteDefinition> map)
    {
        this.Key = key;
        this.Path = path;
        this.EndpointName = endpointName;
        this.Title = title;
        this.Description = description;
        this.Map = map;
    }

    public string Key { get; }

    public string Path { get; }

    public string EndpointName { get; }

    public string Title { get; }

    public string Description { get; }

    public Action<RouteGroupBuilder, DashboardPageDefinition, DashboardPageRouteDefinition> Map { get; }

    public static DashboardPageRouteDefinition CreateTyped<TPage>(
        string key,
        string path,
        string endpointName,
        string title,
        string description)
        where TPage : IRazorSliceProxy
    {
        return new DashboardPageRouteDefinition(
            key,
            path,
            endpointName,
            title,
            description,
            (group, _, route) => group.MapDashboardPage<TPage>(
                route.Path,
                route.EndpointName,
                route.Title,
                route.Description));
    }

    public static DashboardPageRouteDefinition CreatePathBased(
        string key,
        string path,
        string razorIdentifier,
        Assembly assembly,
        string endpointName,
        string title,
        string description)
    {
        return new DashboardPageRouteDefinition(
            key,
            path,
            endpointName,
            title,
            description,
            (group, _, route) => group.MapDashboardPage(
                route.Path,
                razorIdentifier,
                assembly,
                route.EndpointName,
                route.Title,
                route.Description));
    }
}

internal sealed class DashboardPageActionDefinition(string[] methods, string path, Delegate handler)
{
    public string[] Methods { get; } = methods;

    public string Path { get; } = path;

    public Delegate Handler { get; } = handler;

    public string Key { get; set; }

    public string Name { get; set; }

    public string Summary { get; set; }

    public string Description { get; set; }
}

