// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Contributes the Entities page to the bITdevKit dashboard when at least one entity source is registered.
/// </summary>
/// <example>
/// <code>
/// // registered automatically by AddDashboardPlugins(); the page is available at {dashboardGroup}/entities
/// services.AddEntitiesDashboard(o => o.AddRepository&lt;Invoice, InvoiceId&gt;());
/// </code>
/// </example>
public sealed class DashboardPageProvider(DashboardEndpointsOptions dashboardOptions) : IDashboardPageProvider
{
    /// <inheritdoc />
    public IEnumerable<DashboardPage> GetPages(HttpContext context)
    {
        var catalog = context?.RequestServices.GetService<EntitiesDashboardCatalog>();
        if (catalog is not { HasSources: true } || dashboardOptions?.DisabledPageKeys?.Contains("entities") == true)
        {
            yield break;
        }

        yield return new DashboardPage(
            "entities",
            "Entities",
            "table",
            DashboardEndpoints.BuildEntitiesPath(dashboardOptions))
        {
            Group = "bdk",
            GroupOrder = 0,
            Order = 66,
            Description = "Browse, search and maintain repository and Active Entity data.",
            Card = GetCardAsync
        };
    }

    private ValueTask<DashboardPageCard> GetCardAsync(HttpContext context)
    {
        var url = DashboardEndpoints.BuildEntitiesPath(dashboardOptions);

        try
        {
            var catalog = context.RequestServices.GetService<EntitiesDashboardCatalog>();
            if (catalog is not { HasSources: true })
            {
                return ValueTask.FromResult(CreateCard("Unavailable", "No entity sources registered", url));
            }

            var sources = catalog.Entities.SelectMany(e => e.Sources).ToList();
            var repositories = sources.Count(s => s.SourceName == "Repository");
            var activeEntities = sources.Count(s => s.SourceName == "ActiveEntity");

            return ValueTask.FromResult(CreateCard(
                catalog.Entities.Count.ToString(),
                $"Repository: {repositories} · ActiveEntity: {activeEntities}",
                url));
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return ValueTask.FromResult(CreateCard("Error", "Entity catalog unavailable", url));
        }
    }

    private static DashboardPageCard CreateCard(string value, string detail, string url) =>
        new("Entities", "entity types", value)
        {
            Detail = detail,
            Icon = "table",
            Url = url,
            Group = "bdk",
            GroupOrder = 0,
            Order = 66,
            FeatureKey = "entities"
        };
}
