// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage.Files;

using System.Globalization;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides the File Storage dashboard page descriptor and index card.
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
        yield return new DashboardPage("Files", "folder2-open", DashboardEndpoints.BuildFilesPath(options))
        {
            Group = "bdk",
            GroupOrder = 0,
            Order = 48,
            Description = "Browse and manage registered file storage locations",
            Tooltip = "File storage locations",
            Card = GetCardAsync
        };
    }

    private static ValueTask<DashboardPageCard> GetCardAsync(HttpContext context)
    {
        var factory = context.RequestServices.GetService<IFileStorageProviderFactory>();
        var url = DashboardEndpoints.BuildFilesPath(context.RequestServices.GetRequiredService<DashboardEndpointsOptions>());

        if (factory is null)
        {
            return ValueTask.FromResult(CreateCard("Unavailable", "AddFileStorage() is not registered", url));
        }

        try
        {
            var providers = factory.GetProviderNames();
            var detail = providers.Count == 0
                ? "No storage locations registered"
                : string.Join(", ", providers.Take(3)) + (providers.Count > 3 ? $" +{providers.Count - 3}" : string.Empty);

            return ValueTask.FromResult(CreateCard(providers.Count.ToString("N0", CultureInfo.InvariantCulture), detail, url));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult(CreateCard("Error", ex.Message, url));
        }
    }

    private static DashboardPageCard CreateCard(string value, string detail, string url) =>
        new("Files", "Storage locations", value)
        {
            Detail = detail,
            Icon = "folder2-open",
            Url = url,
            Group = "bdk",
            GroupOrder = 0,
            Order = 48
        };
}
