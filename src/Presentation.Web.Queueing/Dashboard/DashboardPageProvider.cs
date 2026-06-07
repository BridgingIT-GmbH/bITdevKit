// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Queueing.Dashboard;

using System.Globalization;
using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides the Queueing dashboard page descriptor and index card.
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
        yield return new DashboardPage("Queueing", "inboxes", DashboardEndpoints.BuildQueueingPath(options))
        {
            Group = "bdk",
            GroupOrder = 0,
            Order = 46,
            Description = "Retained queue messages, queues, and queue subscriptions",
            Tooltip = "Queue messages and subscriptions",
            Card = GetCardAsync
        };
    }

    private static async ValueTask<DashboardPageCard> GetCardAsync(HttpContext context)
    {
        var service = context.RequestServices.GetService<IQueueBrokerService>();
        var url = DashboardEndpoints.BuildQueueingPath(context.RequestServices.GetRequiredService<DashboardEndpointsOptions>());

        if (service is null)
        {
            return CreateCard("Unavailable", "AddQueueing() is not registered", url);
        }

        try
        {
            var stats = await service.GetMessageStatsAsync(isArchived: false, cancellationToken: context.RequestAborted);
            var detail = $"{stats.Pending.ToString("N0", CultureInfo.InvariantCulture)} pending, {stats.WaitingForHandler.ToString("N0", CultureInfo.InvariantCulture)} waiting, {stats.Failed.ToString("N0", CultureInfo.InvariantCulture)} failed";

            return CreateCard(stats.Total.ToString("N0", CultureInfo.InvariantCulture), detail, url);
        }
        catch (Exception ex)
        {
            return CreateCard("Error", ex.Message, url);
        }
    }

    private static DashboardPageCard CreateCard(string value, string detail, string url) =>
        new("Queueing", "Queue messages", value)
        {
            Detail = detail,
            Icon = "inboxes",
            Url = url,
            Group = "bdk",
            GroupOrder = 0,
            Order = 46
        };
}
