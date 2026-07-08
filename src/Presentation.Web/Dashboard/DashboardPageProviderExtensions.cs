// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides safe helpers for dashboard page providers.
/// </summary>
/// <example>
/// <code>
/// var pages = httpContext.GetDashboardPages();
/// </code>
/// </example>
public static class DashboardPageProviderExtensions
{
    /// <summary>
    /// Gets all dashboard pages from registered providers without allowing one provider failure to break the shell.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <returns>The available dashboard pages.</returns>
    public static IReadOnlyList<DashboardPage> GetDashboardPages(this HttpContext httpContext)
    {
        var logger = httpContext.RequestServices.GetService<ILogger<IDashboardPageProvider>>();
        var options = httpContext.RequestServices.GetService<DashboardEndpointsOptions>();
        var pages = new List<DashboardPage>();

        foreach (var provider in httpContext.RequestServices.GetServices<IDashboardPageProvider>())
        {
            try
            {
                pages.AddRange(provider.GetPages(httpContext)
                    .Where(page => page is not null)
                    .Where(page => IsEnabled(options, page)));
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Dashboard page provider failed (type={DashboardPageProviderType})", provider.GetType().FullName);
            }
        }

        return pages;
    }

    private static bool IsEnabled(DashboardEndpointsOptions options, DashboardPage page)
    {
        return options?.DisabledPageKeys?.Contains(page.Key) != true;
    }

    /// <summary>
    /// Gets dashboard index cards from pages that choose to provide one.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <returns>The dashboard index cards.</returns>
    public static async Task<IReadOnlyList<DashboardPageCard>> GetDashboardPageCardsAsync(this HttpContext httpContext)
    {
        var logger = httpContext.RequestServices.GetService<ILogger<DashboardPageCard>>();
        var cards = new List<DashboardPageCard>();

        foreach (var page in httpContext.GetDashboardPages().Where(page => page.ShowOnIndex))
        {
            try
            {
                var card = page.Card is null
                    ? CreateDefaultCard(page)
                    : await page.Card(httpContext);

                if (card is not null)
                {
                    cards.Add(card);
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Dashboard page card provider failed (page={DashboardPageTitle})", page.Title);
                cards.Add(new DashboardPageCard(page.Title)
                {
                    Detail = "Card unavailable",
                    Icon = page.Icon,
                    Url = page.Url,
                    Group = page.Group,
                    GroupOrder = page.GroupOrder,
                    Order = page.Order
                });
            }
        }

        return cards;
    }

    private static DashboardPageCard CreateDefaultCard(DashboardPage page)
    {
        return new DashboardPageCard(page.Title)
        {
            Icon = page.Icon,
            Url = page.Url,
            Group = page.Group,
            GroupOrder = page.GroupOrder,
            Order = page.Order
        };
    }
}
