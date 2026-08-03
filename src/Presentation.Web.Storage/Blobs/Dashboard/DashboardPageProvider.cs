// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage.Blobs.Dashboard;

using System.Globalization;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides the Blob Storage dashboard page descriptor and index card.
/// </summary>
/// <example>
/// <code>
/// var pages = provider.GetPages(httpContext);
/// </code>
/// </example>
public sealed class DashboardPageProvider(DashboardEndpointsOptions options) : IDashboardPageProvider
{
    /// <inheritdoc />
    public IEnumerable<DashboardPage> GetPages(HttpContext httpContext)
    {
        var factory = httpContext.RequestServices.GetService<IBlobStoreClientFactory>();
        if (factory is null || factory.GetRegistrations().Count == 0)
        {
            yield break;
        }

        yield return new DashboardPage("storage.blobs", "Blobs", "database-fill", BlobStorageDashboardRoutes.BuildBlobsPath(options))
        {
            Group = "bdk",
            GroupOrder = 0,
            Order = 50,
            Description = "Browse and manage registered blob storage clients",
            Tooltip = "Blob storage clients",
            Card = GetCardAsync
        };
    }

    private static ValueTask<DashboardPageCard> GetCardAsync(HttpContext context)
    {
        var factory = context.RequestServices.GetService<IBlobStoreClientFactory>();
        var url = BlobStorageDashboardRoutes.BuildBlobsPath(context.RequestServices.GetRequiredService<DashboardEndpointsOptions>());

        if (factory is null)
        {
            return ValueTask.FromResult(CreateCard("Unavailable", "AddBlobStorage() is not registered", url));
        }

        try
        {
            var registrations = factory.GetRegistrations().OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();
            var detail = registrations.Length == 0
                ? "No blob clients registered"
                : string.Join(", ", registrations.Take(3).Select(item => item.Name)) +
                  (registrations.Length > 3 ? $" +{registrations.Length - 3}" : string.Empty);

            return ValueTask.FromResult(CreateCard(registrations.Length.ToString("N0", CultureInfo.InvariantCulture), detail, url));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult(CreateCard("Error", ex.Message, url));
        }
    }

    private static DashboardPageCard CreateCard(string value, string detail, string url) =>
        new("Blobs", "Blob clients", value)
        {
            Detail = detail,
            Icon = "database-fill",
            Url = url,
            Group = "bdk",
            GroupOrder = 0,
            Order = 50
        };
}
