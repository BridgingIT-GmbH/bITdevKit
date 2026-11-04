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
    private const string McpStatusPath = "/mcp/status";

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

        foreach (var view in new[] { "overview", "connections", "handlers", "operations" })
        {
            var suffix = view == "overview" ? string.Empty : $"/{view}";
            var name = view == "overview" ? "Mcp" : $"Mcp.{view}";
            group.MapDashboardPage<Pages.Mcp>(
                McpPath + suffix,
                $"_bdk.Dashboard.{name}",
                "Dashboard MCP",
                "Shows local MCP runtime, IPC, and handler diagnostics.");

            group.MapDashboardPage<Pages.McpContent>(
                McpPath + suffix + "/content",
                $"_bdk.Dashboard.{name}Content",
                "Dashboard MCP Content",
                "Shows the selected MCP dashboard content fragment.");
        }

        group.MapGet(McpStatusPath, (HttpContext context) =>
            {
                var status = McpDashboardStatus.Get(context.RequestServices);

                return Results.Json(new { status = status.Status, connected = status.IsConnected, title = status.Title });
            })
            .WithName("_bdk.Dashboard.McpStatus")
            .WithSummary("Dashboard MCP Status")
            .WithDescription("Returns the MCP server connection state shown in the dashboard header.")
            .Produces(StatusCodes.Status200OK);
    }

    internal static string BuildMcpPath(DashboardEndpointsOptions options, string view = "overview")
    {
        return DashboardPath.Combine(options?.GroupPath, McpPath + (view == "overview" ? string.Empty : $"/{view}"));
    }

    internal static string BuildMcpStatusPath(DashboardEndpointsOptions options)
    {
        return DashboardPath.Combine(options?.GroupPath, McpStatusPath);
    }

    internal static string BuildMcpContentPath(DashboardEndpointsOptions options, string view = "overview")
    {
        return BuildMcpPath(options, view) + "/content";
    }

    internal static string GetView(HttpContext context)
    {
        var path = context.Request.Path.Value?.TrimEnd('/') ?? string.Empty;
        return new[] { "connections", "handlers", "operations" }.FirstOrDefault(view =>
            path.EndsWith($"/mcp/{view}", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith($"/mcp/{view}/content", StringComparison.OrdinalIgnoreCase)) ?? "overview";
    }
}
