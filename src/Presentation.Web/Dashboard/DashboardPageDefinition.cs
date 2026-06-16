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

    /// <summary>
    /// Gets or sets the main dashboard page route definition.
    /// </summary>
    /// <example>
    /// <code>
    /// definition.Route = DashboardPageRouteDefinition.CreateTyped&lt;Pages.Overview&gt;("page", "/overview", "Overview", "Overview", null);
    /// </code>
    /// </example>
    public DashboardPageRouteDefinition Route { get; set; }

    /// <summary>
    /// Gets the refreshable fragment route definitions for the page.
    /// </summary>
    /// <example>
    /// <code>
    /// definition.Fragments.Add(fragment);
    /// </code>
    /// </example>
    public List<DashboardPageRouteDefinition> Fragments { get; } = [];

    /// <summary>
    /// Gets the page-local action route definitions.
    /// </summary>
    /// <example>
    /// <code>
    /// definition.Actions.Add(action);
    /// </code>
    /// </example>
    public List<DashboardPageActionDefinition> Actions { get; } = [];
}

/// <summary>
/// Describes a RazorSlice route owned by a dashboard page.
/// </summary>
/// <example>
/// <code>
/// var route = DashboardPageRouteDefinition.CreateTyped&lt;Pages.Overview&gt;("page", "/overview", "Overview", "Overview", null);
/// </code>
/// </example>
public sealed class DashboardPageRouteDefinition
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

    /// <summary>
    /// Gets the stable route key.
    /// </summary>
    /// <example>
    /// <code>
    /// var key = route.Key;
    /// </code>
    /// </example>
    public string Key { get; }

    /// <summary>
    /// Gets the route path below the dashboard group path.
    /// </summary>
    /// <example>
    /// <code>
    /// var path = route.Path;
    /// </code>
    /// </example>
    public string Path { get; }

    /// <summary>
    /// Gets the endpoint name.
    /// </summary>
    /// <example>
    /// <code>
    /// var endpointName = route.EndpointName;
    /// </code>
    /// </example>
    public string EndpointName { get; }

    /// <summary>
    /// Gets the endpoint title.
    /// </summary>
    /// <example>
    /// <code>
    /// var title = route.Title;
    /// </code>
    /// </example>
    public string Title { get; }

    /// <summary>
    /// Gets the endpoint description.
    /// </summary>
    /// <example>
    /// <code>
    /// var description = route.Description;
    /// </code>
    /// </example>
    public string Description { get; }

    /// <summary>
    /// Gets the route mapping delegate.
    /// </summary>
    /// <example>
    /// <code>
    /// route.Map(group, definition, route);
    /// </code>
    /// </example>
    public Action<RouteGroupBuilder, DashboardPageDefinition, DashboardPageRouteDefinition> Map { get; }

    /// <summary>
    /// Creates a route definition for a typed RazorSlice.
    /// </summary>
    /// <typeparam name="TPage">The generated RazorSlice proxy type.</typeparam>
    /// <param name="key">The stable route key.</param>
    /// <param name="path">The route path below the dashboard group path.</param>
    /// <param name="endpointName">The endpoint name.</param>
    /// <param name="title">The endpoint title.</param>
    /// <param name="description">The endpoint description.</param>
    /// <returns>A dashboard page route definition.</returns>
    /// <example>
    /// <code>
    /// var route = DashboardPageRouteDefinition.CreateTyped&lt;Pages.Overview&gt;("page", "/overview", "Overview", "Overview", null);
    /// </code>
    /// </example>
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

    /// <summary>
    /// Creates a route definition for a path-based compiled RazorSlice.
    /// </summary>
    /// <param name="key">The stable route key.</param>
    /// <param name="path">The route path below the dashboard group path.</param>
    /// <param name="razorIdentifier">The compiled Razor item identifier.</param>
    /// <param name="assembly">The assembly containing the RazorSlice.</param>
    /// <param name="endpointName">The endpoint name.</param>
    /// <param name="title">The endpoint title.</param>
    /// <param name="description">The endpoint description.</param>
    /// <returns>A dashboard page route definition.</returns>
    /// <example>
    /// <code>
    /// var route = DashboardPageRouteDefinition.CreatePathBased("page", "/overview", "/Pages/Overview.cshtml", typeof(App).Assembly, "Overview", "Overview", null);
    /// </code>
    /// </example>
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

/// <summary>
/// Describes an action route owned by a dashboard page.
/// </summary>
/// <param name="methods">The HTTP methods.</param>
/// <param name="path">The route path below the page path.</param>
/// <param name="handler">The Minimal API handler delegate.</param>
/// <example>
/// <code>
/// var action = new DashboardPageActionDefinition(["POST"], "/create", CreateAsync);
/// </code>
/// </example>
public sealed class DashboardPageActionDefinition(string[] methods, string path, Delegate handler)
{
    /// <summary>
    /// Gets the HTTP methods.
    /// </summary>
    /// <example>
    /// <code>
    /// var methods = action.Methods;
    /// </code>
    /// </example>
    public string[] Methods { get; } = methods;

    /// <summary>
    /// Gets the route path below the page path.
    /// </summary>
    /// <example>
    /// <code>
    /// var path = action.Path;
    /// </code>
    /// </example>
    public string Path { get; } = path;

    /// <summary>
    /// Gets the Minimal API handler delegate.
    /// </summary>
    /// <example>
    /// <code>
    /// var handler = action.Handler;
    /// </code>
    /// </example>
    public Delegate Handler { get; } = handler;

    /// <summary>
    /// Gets or sets the stable action key.
    /// </summary>
    /// <example>
    /// <code>
    /// action.Key = "create";
    /// </code>
    /// </example>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the endpoint name.
    /// </summary>
    /// <example>
    /// <code>
    /// action.Name = "Core.Cities.Create";
    /// </code>
    /// </example>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the endpoint summary.
    /// </summary>
    /// <example>
    /// <code>
    /// action.Summary = "Create city";
    /// </code>
    /// </example>
    public string Summary { get; set; }

    /// <summary>
    /// Gets or sets the endpoint description.
    /// </summary>
    /// <example>
    /// <code>
    /// action.Description = "Creates a city from the dashboard.";
    /// </code>
    /// </example>
    public string Description { get; set; }
}
