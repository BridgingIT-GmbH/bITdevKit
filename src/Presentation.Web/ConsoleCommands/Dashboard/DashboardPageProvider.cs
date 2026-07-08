// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.ConsoleCommands.Dashboard;

using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides the dashboard navigation entry for the web console.
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
        if (httpContext.RequestServices.GetService<ConsoleCommandExecutor>() is null)
        {
            yield break;
        }

        yield return new DashboardPage("console", "Console", "terminal", DashboardEndpoints.BuildConsolePath(options))
        {
            Group = "bdk",
            GroupOrder = 0,
            Order = 50,
            Description = "Run console commands",
            Card = context => ValueTask.FromResult(new DashboardPageCard("Console", "Interactive command runner", "Ready")
            {
                Detail = "Run registered console commands from the dashboard",
                Icon = "terminal",
                Url = DashboardEndpoints.BuildConsolePath(options),
                Group = "bdk",
                GroupOrder = 0,
                Order = 50
            })
        };
    }
}
