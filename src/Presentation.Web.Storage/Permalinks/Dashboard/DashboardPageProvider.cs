// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web.Storage.Permalinks.Dashboard;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides the Storage Permalink Registry dashboard navigation item.
/// </summary>
/// <example>
/// <code>
/// var pages = provider.GetPages(context);
/// </code>
/// </example>
public sealed class DashboardPageProvider(DashboardEndpointsOptions options) : IDashboardPageProvider
{
    /// <inheritdoc />
    public IEnumerable<DashboardPage> GetPages(HttpContext context)
    {
        if (context.RequestServices.GetService<IStoragePermalinkRegistryProvider>() is null) yield break;
        yield return new DashboardPage("storage.permalinks", "Permalinks", "link-45deg", StoragePermalinkDashboardRoutes.Index(options))
        {
            Group = "bdk", GroupOrder = 0, Order = 52, Description = "Manage stable storage download links", Tooltip = "Storage permalinks",
            Card = _ => ValueTask.FromResult(new DashboardPageCard("Permalinks", "Storage registry", "Open") { Detail = "List, expire, or delete links", Icon = "link-45deg", Url = StoragePermalinkDashboardRoutes.Index(options), Group = "bdk", GroupOrder = 0, Order = 52 })
        };
    }
}
