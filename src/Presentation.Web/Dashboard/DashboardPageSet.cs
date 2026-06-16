// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

using System.Net;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

/// <summary>
/// Provides a base class for dashboard modules that define multiple pages, fragments, and actions in one place.
/// </summary>
/// <example>
/// <code>
/// public sealed class CoreDashboard(DashboardEndpointsOptions options) : DashboardPageSet(options)
/// {
///     protected override void Configure(DashboardPageSetBuilder pages)
///     {
///         pages.Group("Core", 100)
///             .Page("overview", "/app/core")
///             .Title("Overview")
///             .Icon("cloud-sun")
///             .Razor("/Modules/Core/Dashboard/Pages/Overview/Index.cshtml", typeof(CoreDashboard).Assembly);
///     }
/// }
/// </code>
/// </example>
public abstract class DashboardPageSet(DashboardEndpointsOptions options)
    : EndpointsBase, IDashboardEndpoints, IDashboardPageProvider
{
    private readonly object gate = new();
    private IReadOnlyList<DashboardPageDefinition> definitions;
    private string tag;

    /// <summary>
    /// Configures the dashboard pages owned by this page set.
    /// </summary>
    /// <param name="pages">The dashboard page set builder.</param>
    protected abstract void Configure(DashboardPageSetBuilder pages);

    /// <inheritdoc />
    public override void Map(IEndpointRouteBuilder app)
    {
        var dashboardOptions = options ?? new DashboardEndpointsOptions();
        if (!dashboardOptions.Enabled)
        {
            return;
        }

        var group = this.MapGroup(app, dashboardOptions);
        if (!string.IsNullOrWhiteSpace(this.GetTag()))
        {
            group.WithTags(this.GetTag());
        }

        foreach (var page in this.GetDefinitions())
        {
            page.Route?.Map(group, page, page.Route);

            foreach (var fragment in page.Fragments)
            {
                fragment.Map(group, page, fragment);
            }

            foreach (var action in page.Actions)
            {
                var route = DashboardPath.Combine(page.Path, action.Path);
                group.MapMethods(route, action.Methods, action.Handler)
                    .WithName(action.Name ?? this.BuildEndpointName(page, action.Key))
                    .WithSummary(action.Summary ?? $"{page.Title} {action.Key}")
                    .WithDescription(action.Description ?? $"Runs the {action.Key} dashboard action for {page.Title}.")
                    .Produces<string>((int)HttpStatusCode.OK);
            }
        }
    }

    /// <inheritdoc />
    public IEnumerable<DashboardPage> GetPages(HttpContext httpContext)
    {
        foreach (var page in this.GetDefinitions())
        {
            yield return new DashboardPage(page.Title, page.Icon, this.GetUrl(page.Key))
            {
                Group = page.Group,
                GroupOrder = page.GroupOrder,
                Order = page.Order,
                Description = page.Description,
                Tooltip = page.Tooltip,
                ShowInSidebar = page.ShowInSidebar,
                ShowOnIndex = page.ShowOnIndex,
                Badge = page.Badge,
                Card = page.Card is null
                    ? null
                    : context => page.Card(new DashboardPageCardContext(context, page, this.GetUrl(page.Key)))
            };
        }
    }

    /// <summary>
    /// Gets the absolute dashboard URL for a page or page fragment.
    /// </summary>
    /// <param name="pageKey">The page key.</param>
    /// <param name="fragmentKey">The optional fragment key.</param>
    /// <returns>The absolute dashboard URL, or <c>null</c> when the key is unknown.</returns>
    public string GetUrl(string pageKey, string fragmentKey = null)
    {
        if (string.IsNullOrWhiteSpace(pageKey))
        {
            return null;
        }

        var page = this.GetDefinitions()
            .FirstOrDefault(p => string.Equals(p.Key, pageKey, StringComparison.OrdinalIgnoreCase));
        if (page is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(fragmentKey))
        {
            return DashboardPath.Combine((options ?? new DashboardEndpointsOptions()).GroupPath, page.Path);
        }

        var fragment = page.Fragments
            .FirstOrDefault(f => string.Equals(f.Key, fragmentKey, StringComparison.OrdinalIgnoreCase));

        return fragment is null
            ? null
            : DashboardPath.Combine((options ?? new DashboardEndpointsOptions()).GroupPath, fragment.Path);
    }

    /// <summary>
    /// Gets the page definitions configured by this page set.
    /// </summary>
    /// <returns>The configured dashboard page definitions.</returns>
    /// <example>
    /// <code>
    /// var definitions = dashboard.GetDefinitions();
    /// </code>
    /// </example>
    public IReadOnlyList<DashboardPageDefinition> GetDefinitions()
    {
        if (this.definitions is not null)
        {
            return this.definitions;
        }

        lock (this.gate)
        {
            if (this.definitions is not null)
            {
                return this.definitions;
            }

            var builder = new DashboardPageSetBuilder(this.BuildEndpointName);
            this.Configure(builder);
            this.definitions = builder.Build();
            this.tag = builder.Tag;
        }

        return this.definitions;
    }

    private string GetTag()
    {
        _ = this.GetDefinitions();
        return this.tag;
    }

    private string BuildEndpointName(DashboardPageDefinition page, string endpointKey)
    {
        return $"{(options ?? new DashboardEndpointsOptions()).GroupTag}.{this.GetType().Name}.{ToEndpointToken(page.Key)}{ToEndpointToken(endpointKey)}";
    }

    private static string ToEndpointToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "page", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var parts = value
            .Split(['-', '_', '.', '/', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return string.Concat(parts.Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }
}
