// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.EntityFramework.ChangeHistory.Dashboard;

using BridgingIT.DevKit.Presentation.Web.Dashboard;
using BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

/// <summary>
/// Links Entities dashboard rows to the Change History dashboard page for tracked entity types.
/// </summary>
/// <example>
/// <code>
/// services.AddChangeHistoryDashboard&lt;Customer, AppDbContext&gt;();
/// // The Entities page now shows a "Change history" link for each Customer, for example:
/// // /dashboard/change-history?registration=AppDbContext%3ACustomer&amp;entityId=3f2c
/// </code>
/// </example>
public sealed class ChangeHistoryEntitiesDashboardLinkProvider(
    IServiceProvider services,
    DashboardEndpointsOptions options) : IEntitiesDashboardChangeHistoryLinkProvider
{
    /// <inheritdoc />
    /// <example>
    /// <code>
    /// var link = provider.GetLink(typeof(Customer), customerId);
    /// </code>
    /// </example>
    public string GetLink(Type entityType, string entityId)
    {
        if (entityType is null || string.IsNullOrWhiteSpace(entityId))
        {
            return null;
        }

        if (options is { Enabled: false } ||
            options?.DisabledPageKeys.Contains("change-history") == true)
        {
            return null;
        }

        var descriptor = ChangeHistoryDashboardDiscovery.GetRegistrations(services)
            .FirstOrDefault(d => d.EntityType == entityType);
        if (descriptor is null)
        {
            return null;
        }

        return $"{DashboardEndpoints.BuildChangeHistoryPath(options)}" +
            $"?registration={Uri.EscapeDataString(descriptor.Key)}&entityId={Uri.EscapeDataString(entityId)}";
    }
}
