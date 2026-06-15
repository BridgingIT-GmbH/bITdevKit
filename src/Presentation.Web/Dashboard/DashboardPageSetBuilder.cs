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
/// Builds dashboard page definitions for a module-level <see cref="DashboardPageSet" />.
/// </summary>
/// <example>
/// <code>
/// pages.Group("Core", 100)
///     .Page("overview", "/app/core")
///     .Title("Overview")
///     .Icon("cloud-sun");
/// </code>
/// </example>
public sealed class DashboardPageSetBuilder(Func<DashboardPageDefinition, string, string> endpointNameFactory)
{
    private readonly List<DashboardPageDefinition> pages = [];

    /// <summary>
    /// Gets the optional endpoint tag for all routes mapped by the page set.
    /// </summary>
    public string Tag { get; private set; }

    /// <summary>
    /// Sets an additional endpoint tag for all routes mapped by the page set.
    /// </summary>
    /// <param name="tag">The endpoint tag.</param>
    /// <returns>The current builder.</returns>
    public DashboardPageSetBuilder WithTags(string tag)
    {
        this.Tag = tag;
        return this;
    }

    /// <summary>
    /// Starts a grouped set of dashboard pages.
    /// </summary>
    /// <param name="name">The sidebar and card group name.</param>
    /// <param name="order">The group order.</param>
    /// <returns>A group builder.</returns>
    public DashboardPageGroupBuilder Group(string name, int order = 0)
    {
        return new DashboardPageGroupBuilder(this, name, order);
    }

    /// <summary>
    /// Adds a page to the default dashboard group.
    /// </summary>
    /// <param name="key">The stable page key.</param>
    /// <param name="path">The route path below the dashboard group path.</param>
    /// <returns>A page builder.</returns>
    public DashboardPageBuilder Page(string key, string path)
    {
        return this.Group("bdk").Page(key, path);
    }

    internal IReadOnlyList<DashboardPageDefinition> Build()
    {
        return this.pages.ToArray();
    }

    internal DashboardPageBuilder AddPage(string group, int groupOrder, string key, string path)
    {
        var page = new DashboardPageDefinition(key, path)
        {
            Group = string.IsNullOrWhiteSpace(group) ? "bdk" : group,
            GroupOrder = groupOrder
        };

        this.pages.Add(page);
        return new DashboardPageBuilder(this, page, endpointNameFactory);
    }
}

/// <summary>
/// Builds dashboard pages that share a sidebar/card group.
/// </summary>
/// <example>
/// <code>
/// var core = pages.Group("Core", 100);
/// core.Page("overview", "/app/core").Title("Overview");
/// </code>
/// </example>
public sealed class DashboardPageGroupBuilder(
    DashboardPageSetBuilder owner,
    string group,
    int groupOrder)
{
    /// <summary>
    /// Adds a page to this group.
    /// </summary>
    /// <param name="key">The stable page key.</param>
    /// <param name="path">The route path below the dashboard group path.</param>
    /// <returns>A page builder.</returns>
    public DashboardPageBuilder Page(string key, string path)
    {
        return owner.AddPage(group, groupOrder, key, path);
    }
}

/// <summary>
/// Builds one dashboard page, its refreshable fragments, card, badge, and local action routes.
/// </summary>
/// <example>
/// <code>
/// pages.Group("Core")
///     .Page("cities", "/app/core/cities")
///     .Title("City Management")
///     .Razor("/Modules/Core/Dashboard/Pages/Cities/Index.cshtml", typeof(CoreDashboard).Assembly);
/// </code>
/// </example>
public sealed class DashboardPageBuilder(
    DashboardPageSetBuilder owner,
    DashboardPageDefinition page,
    Func<DashboardPageDefinition, string, string> endpointNameFactory)
{
    /// <summary>
    /// Adds another page to the same group.
    /// </summary>
    /// <param name="key">The stable page key.</param>
    /// <param name="path">The route path below the dashboard group path.</param>
    /// <returns>A page builder.</returns>
    public DashboardPageBuilder Page(string key, string path)
    {
        return owner.AddPage(page.Group, page.GroupOrder, key, path);
    }

    /// <summary>
    /// Sets the page title.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <returns>The current page builder.</returns>
    public DashboardPageBuilder Title(string title)
    {
        page.Title = title;
        return this;
    }

    /// <summary>
    /// Sets the Bootstrap icon name without the <c>bi-</c> prefix.
    /// </summary>
    /// <param name="icon">The icon name.</param>
    /// <returns>The current page builder.</returns>
    public DashboardPageBuilder Icon(string icon)
    {
        page.Icon = icon;
        return this;
    }

    /// <summary>
    /// Sets the page order inside its group.
    /// </summary>
    /// <param name="order">The page order.</param>
    /// <returns>The current page builder.</returns>
    public DashboardPageBuilder Order(int order)
    {
        page.Order = order;
        return this;
    }

    /// <summary>
    /// Sets the page description.
    /// </summary>
    /// <param name="description">The description.</param>
    /// <returns>The current page builder.</returns>
    public DashboardPageBuilder Description(string description)
    {
        page.Description = description;
        return this;
    }

    /// <summary>
    /// Hides the page from the dashboard sidebar.
    /// </summary>
    /// <returns>The current page builder.</returns>
    public DashboardPageBuilder HideFromSidebar()
    {
        page.ShowInSidebar = false;
        return this;
    }

    /// <summary>
    /// Hides the page from the dashboard index card list.
    /// </summary>
    /// <returns>The current page builder.</returns>
    public DashboardPageBuilder HideFromIndex()
    {
        page.ShowOnIndex = false;
        return this;
    }

    /// <summary>
    /// Maps the main page to a typed RazorSlice.
    /// </summary>
    /// <typeparam name="TPage">The generated RazorSlice type.</typeparam>
    /// <returns>The current page builder.</returns>
    public DashboardPageBuilder Razor<TPage>()
        where TPage : IRazorSliceProxy
    {
        page.Route = DashboardPageRouteDefinition.CreateTyped<TPage>(
            "page",
            page.Path,
            endpointNameFactory(page, "page"),
            page.Title,
            page.Description);

        return this;
    }

    /// <summary>
    /// Maps the main page to a path-based compiled RazorSlice.
    /// </summary>
    /// <param name="razorIdentifier">The compiled Razor item identifier.</param>
    /// <param name="assembly">The assembly containing the RazorSlice.</param>
    /// <returns>The current page builder.</returns>
    public DashboardPageBuilder Razor(string razorIdentifier, Assembly assembly)
    {
        page.Route = DashboardPageRouteDefinition.CreatePathBased(
            "page",
            page.Path,
            razorIdentifier,
            assembly,
            endpointNameFactory(page, "page"),
            page.Title,
            page.Description);

        return this;
    }

    /// <summary>
    /// Maps a refreshable content fragment to a typed RazorSlice.
    /// </summary>
    /// <typeparam name="TPage">The generated RazorSlice type.</typeparam>
    /// <param name="path">The fragment route below the page route.</param>
    /// <param name="key">The stable fragment key.</param>
    /// <returns>The current page builder.</returns>
    public DashboardPageBuilder Content<TPage>(string path = "/content", string key = "content")
        where TPage : IRazorSliceProxy
    {
        page.Fragments.Add(DashboardPageRouteDefinition.CreateTyped<TPage>(
            key,
            DashboardPath.Combine(page.Path, path),
            endpointNameFactory(page, key),
            $"{page.Title} Content",
            $"Shows the refreshable {page.Title} dashboard content."));

        return this;
    }

    /// <summary>
    /// Maps a refreshable content fragment to a path-based compiled RazorSlice.
    /// </summary>
    /// <param name="razorIdentifier">The compiled Razor item identifier.</param>
    /// <param name="assembly">The assembly containing the RazorSlice.</param>
    /// <param name="path">The fragment route below the page route.</param>
    /// <param name="key">The stable fragment key.</param>
    /// <returns>The current page builder.</returns>
    public DashboardPageBuilder Content(
        string razorIdentifier,
        Assembly assembly,
        string path = "/content",
        string key = "content")
    {
        page.Fragments.Add(DashboardPageRouteDefinition.CreatePathBased(
            key,
            DashboardPath.Combine(page.Path, path),
            razorIdentifier,
            assembly,
            endpointNameFactory(page, key),
            $"{page.Title} Content",
            $"Shows the refreshable {page.Title} dashboard content."));

        return this;
    }

    /// <summary>
    /// Sets an optional sidebar badge provider.
    /// </summary>
    /// <param name="badge">The badge provider.</param>
    /// <returns>The current page builder.</returns>
    public DashboardPageBuilder Badge(Func<HttpContext, ValueTask<int?>> badge)
    {
        page.Badge = badge;
        return this;
    }

    /// <summary>
    /// Sets an optional dashboard index card provider.
    /// </summary>
    /// <param name="card">The card provider.</param>
    /// <returns>The current page builder.</returns>
    public DashboardPageBuilder Card(Func<DashboardPageCardContext, ValueTask<DashboardPageCard>> card)
    {
        page.Card = card;
        return this;
    }

    /// <summary>
    /// Maps a GET action route below the page route.
    /// </summary>
    /// <param name="path">The action path below the page route.</param>
    /// <param name="handler">The Minimal API handler delegate.</param>
    /// <returns>An action builder.</returns>
    public DashboardPageActionBuilder Get(string path, Delegate handler)
    {
        return this.Map(["GET"], path, handler);
    }

    /// <summary>
    /// Maps a POST action route below the page route.
    /// </summary>
    /// <param name="path">The action path below the page route.</param>
    /// <param name="handler">The Minimal API handler delegate.</param>
    /// <returns>An action builder.</returns>
    public DashboardPageActionBuilder Post(string path, Delegate handler)
    {
        return this.Map(["POST"], path, handler);
    }

    /// <summary>
    /// Maps a PUT action route below the page route.
    /// </summary>
    /// <param name="path">The action path below the page route.</param>
    /// <param name="handler">The Minimal API handler delegate.</param>
    /// <returns>An action builder.</returns>
    public DashboardPageActionBuilder Put(string path, Delegate handler)
    {
        return this.Map(["PUT"], path, handler);
    }

    /// <summary>
    /// Maps a DELETE action route below the page route.
    /// </summary>
    /// <param name="path">The action path below the page route.</param>
    /// <param name="handler">The Minimal API handler delegate.</param>
    /// <returns>An action builder.</returns>
    public DashboardPageActionBuilder Delete(string path, Delegate handler)
    {
        return this.Map(["DELETE"], path, handler);
    }

    private DashboardPageActionBuilder Map(string[] methods, string path, Delegate handler)
    {
        var action = new DashboardPageActionDefinition(methods, path, handler)
        {
            Key = methods.First().ToLowerInvariant()
        };
        page.Actions.Add(action);

        return new DashboardPageActionBuilder(this, action);
    }
}

/// <summary>
/// Builds metadata for a page-local dashboard action route.
/// </summary>
/// <example>
/// <code>
/// page.Post("/create", CreateAsync).Name("Core.Cities.Create");
/// </code>
/// </example>
public sealed class DashboardPageActionBuilder
{
    private readonly DashboardPageBuilder page;
    private readonly DashboardPageActionDefinition action;

    internal DashboardPageActionBuilder(
        DashboardPageBuilder page,
        DashboardPageActionDefinition action)
    {
        this.page = page;
        this.action = action;
    }

    /// <summary>
    /// Sets the endpoint name.
    /// </summary>
    /// <param name="name">The endpoint name.</param>
    /// <returns>The page builder.</returns>
    public DashboardPageBuilder Name(string name)
    {
        action.Name = name;
        return page;
    }

    /// <summary>
    /// Sets the endpoint summary.
    /// </summary>
    /// <param name="summary">The endpoint summary.</param>
    /// <returns>The page builder.</returns>
    public DashboardPageBuilder Summary(string summary)
    {
        action.Summary = summary;
        return page;
    }

    /// <summary>
    /// Sets the endpoint description.
    /// </summary>
    /// <param name="description">The endpoint description.</param>
    /// <returns>The page builder.</returns>
    public DashboardPageBuilder Description(string description)
    {
        action.Description = description;
        return page;
    }
}
