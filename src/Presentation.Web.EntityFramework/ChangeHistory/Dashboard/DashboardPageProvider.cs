// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.EntityFramework.ChangeHistory.Dashboard;

using System.Globalization;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides the ChangeHistory dashboard page descriptor and index card.
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
        yield return new DashboardPage("change-history", "Change History", "clock-history", DashboardEndpoints.BuildChangeHistoryPath(options))
        {
            Group = "bdk",
            GroupOrder = 0,
            Order = 65,
            Description = "Tracked entity audit rows, change sets, and restore operations",
            Card = GetCardAsync
        };
    }

    private static ValueTask<DashboardPageCard> GetCardAsync(HttpContext context)
    {
        var registrations = context.RequestServices.GetServices<ChangeHistoryDashboardDescriptor>()
            .Where(descriptor => descriptor.IsValid)
            .ToArray();
        var url = DashboardEndpoints.BuildChangeHistoryPath(context.RequestServices.GetRequiredService<DashboardEndpointsOptions>());
        var value = registrations.Length == 0
            ? "Unavailable"
            : registrations.Length.ToString("N0", CultureInfo.InvariantCulture);
        var detail = registrations.Length == 0
            ? "No ChangeHistory endpoints registered"
            : string.Join(", ", registrations.Select(descriptor => descriptor.EntityTypeName).Distinct().Order());

        return ValueTask.FromResult(new DashboardPageCard("Change History", "Tracked entities", value)
        {
            Detail = detail,
            Icon = "clock-history",
            Url = url,
            Group = "bdk",
            GroupOrder = 0,
            Order = 65
        });
    }
}
