// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Presentation.Web.Auditing.Dashboard;

using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Automatically discovered single Auditing navigation item and card, under global dashboard access.</summary>
/// <example><code>services.AddAuditing(); services.AddDashboard(options => options.AllowAnonymous());</code></example>
public sealed class DashboardPageProvider(DashboardEndpointsOptions options = null) : IDashboardPageProvider
{
    /// <inheritdoc />
    public IEnumerable<DashboardPage> GetPages(HttpContext httpContext)
    {
        if (options is not { Enabled: true } || httpContext.RequestServices.GetService<AuditingBuilder>() is null) yield break;
        var path = DashboardPath.Combine(options.GroupPath, "auditing");
        yield return new("auditing", "Auditing", "journal-text", path)
        {
            Group = "bdk", GroupOrder = 0,
            RememberedViews = new Dictionary<string, string>
            {
                ["executions"] = path,
                ["diagnostics"] = path + "/diagnostics"
            },
            Order = 44, Description = "Business executions, outcomes and retained change evidence",
            Card = _ => ValueTask.FromResult(new DashboardPageCard("Auditing", "Business executions", "Review") { FeatureKey = "auditing", Icon = "journal-text", Url = path, Group = "bdk", GroupOrder = 0, Order = 67, Detail = "Search history, inspect outcomes and download scoped evidence" })
        };
    }
}
