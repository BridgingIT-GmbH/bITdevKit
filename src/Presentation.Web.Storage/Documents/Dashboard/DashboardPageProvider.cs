// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage.Documents.Dashboard;

using System.Globalization;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides the Document Storage dashboard page descriptor and index card.
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
        if (httpContext.RequestServices.GetService<DocumentStorageFeature>()?.IsEnabled != true ||
            !httpContext.RequestServices.GetServices<DocumentStoreClientDescriptor>().Any())
        {
            yield break;
        }

        yield return new DashboardPage("storage.documents", "Documents", "file-earmark-code", DashboardEndpoints.BuildDocumentsPath(options))
        {
            Group = "bdk",
            GroupOrder = 0,
            Order = 49,
            Description = "Browse and manage registered document storage clients",
            Tooltip = "Document storage clients",
            Card = GetCardAsync
        };
    }

    private static ValueTask<DashboardPageCard> GetCardAsync(HttpContext context)
    {
        var descriptors = context.RequestServices.GetServices<DocumentStoreClientDescriptor>().ToArray();
        var url = DashboardEndpoints.BuildDocumentsPath(context.RequestServices.GetRequiredService<DashboardEndpointsOptions>());

        if (context.RequestServices.GetService<DocumentStorageFeature>()?.IsEnabled != true)
        {
            return ValueTask.FromResult(CreateCard("Unavailable", "AddDocumentStorage() is not registered", url));
        }

        var detail = descriptors.Length == 0
            ? "No document clients registered"
            : string.Join(", ", descriptors.Take(3).Select(e => e.DocumentTypeName)) +
              (descriptors.Length > 3 ? $" +{descriptors.Length - 3}" : string.Empty);

        return ValueTask.FromResult(CreateCard(descriptors.Length.ToString("N0", CultureInfo.InvariantCulture), detail, url));
    }

    private static DashboardPageCard CreateCard(string value, string detail, string url) =>
        new("Documents", "Document clients", value)
        {
            Detail = detail,
            Icon = "file-earmark-code",
            Url = url,
            Group = "bdk",
            GroupOrder = 0,
            Order = 49
        };
}
