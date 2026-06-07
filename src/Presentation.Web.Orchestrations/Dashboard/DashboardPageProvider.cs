// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Orchestrations.Dashboard;

using System.Globalization;
using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides the Orchestrations dashboard page descriptor and index card.
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
        yield return new DashboardPage("Orchestrations", "diagram-3", DashboardEndpoints.BuildOrchestrationsPath(options))
        {
            Group = "bdk",
            GroupOrder = 0,
            Order = 47,
            Description = "Persisted orchestration instances and operational controls",
            Tooltip = "Orchestration instances and controls",
            Card = GetCardAsync
        };
    }

    private static async ValueTask<DashboardPageCard> GetCardAsync(HttpContext context)
    {
        var query = context.RequestServices.GetService<IOrchestrationQueryService>();
        var url = DashboardEndpoints.BuildOrchestrationsPath(context.RequestServices.GetRequiredService<DashboardEndpointsOptions>());

        if (query is null)
        {
            return CreateCard("Unavailable", "AddOrchestrations() is not registered", url);
        }

        try
        {
            var result = await query.GetMetricsAsync(cancellationToken: context.RequestAborted);
            if (result.IsFailure)
            {
                return CreateCard("Error", result.Errors.FirstOrDefault()?.Message, url);
            }

            var metrics = result.Value;
            var problem = metrics.FailedCount + metrics.CancelledCount + metrics.TerminatedCount;
            var detail = $"{metrics.RunningCount.ToString("N0", CultureInfo.InvariantCulture)} running, {metrics.WaitingCount.ToString("N0", CultureInfo.InvariantCulture)} waiting, {problem.ToString("N0", CultureInfo.InvariantCulture)} problem";

            return CreateCard(metrics.TotalCount.ToString("N0", CultureInfo.InvariantCulture), detail, url);
        }
        catch (Exception ex)
        {
            return CreateCard("Error", ex.Message, url);
        }
    }

    private static DashboardPageCard CreateCard(string value, string detail, string url) =>
        new("Orchestrations", "Instances", value)
        {
            Detail = detail,
            Icon = "diagram-3",
            Url = url,
            Group = "bdk",
            GroupOrder = 0,
            Order = 47
        };
}
