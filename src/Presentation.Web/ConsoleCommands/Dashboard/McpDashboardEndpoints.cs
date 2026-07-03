// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.ConsoleCommands.Dashboard;

using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

/// <summary>
/// Maps the local MCP dashboard page when MCP is enabled.
/// </summary>
/// <example>
/// <code>
/// services.AddDashboard(options => options.WithPluginAssemblyContaining&lt;McpDashboardEndpoints&gt;());
/// </code>
/// </example>
public sealed class McpDashboardEndpoints(DashboardEndpointsOptions options) : EndpointsBase, IDashboardEndpoints
{
    private const string McpPath = "/mcp";
    private const string McpContentPath = "/mcp/content";

    /// <inheritdoc />
    public override void Map(IEndpointRouteBuilder app)
    {
        options ??= new DashboardEndpointsOptions();

        if (!options.Enabled || !McpDashboardAvailability.IsAvailable(app.ServiceProvider))
        {
            return;
        }

        var group = this.MapGroup(app, options)
            .WithTags("_bdk.Dashboard");

        group.MapDashboardPage<Pages.Mcp>(
            McpPath,
            "_bdk.Dashboard.Mcp",
            "Dashboard MCP",
            "Shows local MCP runtime, IPC, and handler diagnostics.");

        group.MapDashboardPage<Pages.McpContent>(
            McpContentPath,
            "_bdk.Dashboard.McpContent",
            "Dashboard MCP Content",
            "Shows the MCP dashboard content fragment.");
    }

    internal static string BuildMcpPath(DashboardEndpointsOptions options)
    {
        return DashboardPath.Combine(options?.GroupPath, McpPath);
    }

    internal static string BuildMcpContentPath(DashboardEndpointsOptions options)
    {
        return DashboardPath.Combine(options?.GroupPath, McpContentPath);
    }
}
