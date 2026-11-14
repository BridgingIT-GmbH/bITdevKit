// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Caching.Dashboard;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Contributes the independently enabled advanced-cache page, remembered routes, and bounded declaration card.</summary>
/// <example><code>cache.WithDashboard();</code></example>
public sealed class DashboardPageProvider(DashboardEndpointsOptions options = null, CacheCatalog catalog = null,
    CacheDashboardConfiguration configuration = null) : IDashboardPageProvider
{
    /// <inheritdoc />
    public IEnumerable<DashboardPage> GetPages(HttpContext httpContext)
    {
        if (configuration?.Enabled != true || catalog is null || options is not { Enabled: true }) { yield break; }

        var views = CacheDashboardRoutes.GlobalTabs.ToDictionary(tab => tab.Key, tab => CacheDashboardRoutes.Global(options, tab.Key), StringComparer.OrdinalIgnoreCase);
        var scope = httpContext.RequestServices.GetRequiredService<ICacheDashboardAccessContextFactory>().Create(httpContext, CacheAccessActions.Read);
        foreach (var binding in catalog.Collections.Values.Where(binding => scope.Collections is null || scope.Collections.Contains(binding.Name, StringComparer.OrdinalIgnoreCase)))
        {
            foreach (var tab in CacheDashboardRoutes.CollectionTabs)
            {
                views["collection:" + binding.Name + ":" + tab.Key] = CacheDashboardRoutes.Collection(options, binding.Name, tab.Key);
            }
        }

        yield return new("application.caching", "Caching", "lightning-charge", CacheDashboardRoutes.Global(options))
        {
            Group = "bdk", GroupOrder = 0, Order = 47, Description = "Inspect cache collections, shared loads and operations",
            RememberedViews = views, Card = this.CardAsync
        };
    }

    private async ValueTask<DashboardPageCard> CardAsync(HttpContext context)
    {
        var requester = context.RequestServices.GetRequiredService<IRequester>();
        var scope = context.RequestServices.GetRequiredService<ICacheDashboardAccessContextFactory>().Create(context, CacheAccessActions.Read);
        var result = await requester.SendAsync(new CacheCollectionsQuery(scope), cancellationToken: context.RequestAborted).ConfigureAwait(false);
        return new("Caching", "Collections", result.IsSuccess ? result.Value.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Unavailable")
        {
            FeatureKey = "application.caching", Icon = "lightning-charge", Url = CacheDashboardRoutes.Global(options),
            Group = "bdk", GroupOrder = 0, Order = 47
        };
    }
}