// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.ConsoleCommands.Dashboard;

using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Maps dashboard web console endpoints.
/// </summary>
/// <example>
/// <code>
/// services.AddDashboard(o => o.Enabled(true));
/// </code>
/// </example>
public sealed class DashboardEndpoints(DashboardEndpointsOptions options) : EndpointsBase, IDashboardEndpoints
{
    private const string ConsolePath = "/console";
    private const string ConsoleHubPath = "/console/hub";

    /// <inheritdoc />
    public override void Map(IEndpointRouteBuilder app)
    {
        options ??= new DashboardEndpointsOptions();

        if (!options.Enabled || app.ServiceProvider.GetService<ConsoleCommandExecutor>() is null)
        {
            return;
        }

        var group = this.MapGroup(app, options)
            .WithTags("_bdk.Dashboard");

        group.MapDashboardPage<Pages.Index>(
            ConsolePath,
            "_bdk.Dashboard.Console",
            "Dashboard Console",
            "Shows the dashboard web console.");

        group.MapHub<WebConsoleHub>(ConsoleHubPath)
            .WithSummary("Dashboard Console Hub")
            .WithDescription("Streams dashboard web console input and output.");
    }

    internal static string BuildConsolePath(DashboardEndpointsOptions options)
    {
        return DashboardPath.Combine(options?.GroupPath, ConsolePath);
    }

    internal static string BuildConsoleHubPath(DashboardEndpointsOptions options)
    {
        return DashboardPath.Combine(options?.GroupPath, ConsoleHubPath);
    }
}
