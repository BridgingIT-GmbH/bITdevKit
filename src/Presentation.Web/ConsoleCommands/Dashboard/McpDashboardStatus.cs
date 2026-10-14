// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.ConsoleCommands.Dashboard;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Describes the MCP server connection state shown by the dashboard header.
/// </summary>
/// <example>
/// <code>
/// var status = McpDashboardStatus.Get(app.Services);
/// var highlight = status.IsConnected;
/// </code>
/// </example>
internal sealed record McpDashboardStatus(string Status, string Title)
{
    public bool IsConnected => string.Equals(this.Status, "connected", StringComparison.OrdinalIgnoreCase);

    public static McpDashboardStatus Get(IServiceProvider services)
    {
        var snapshot = services?.GetService<McpServerSessionReader>()?.GetSnapshot();

        return snapshot?.Status switch
        {
            "connected" => new("connected", "MCP: connected"),
            "running" => new("running", "MCP: server running, not connected to this runtime"),
            _ => new("offline", "MCP: no server connected")
        };
    }
}
