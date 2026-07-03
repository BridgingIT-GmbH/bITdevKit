// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.ConsoleCommands.Dashboard;

using System.Globalization;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides the dashboard navigation entry for local MCP diagnostics.
/// </summary>
/// <example>
/// <code>
/// var pages = provider.GetPages(httpContext);
/// </code>
/// </example>
public sealed class McpDashboardPageProvider(DashboardEndpointsOptions options) : IDashboardPageProvider
{
    /// <inheritdoc />
    public IEnumerable<DashboardPage> GetPages(HttpContext httpContext)
    {
        if (!McpDashboardAvailability.IsAvailable(httpContext.RequestServices))
        {
            yield break;
        }

        yield return new DashboardPage("MCP", "diagram-3", McpDashboardEndpoints.BuildMcpPath(options))
        {
            Group = "bdk",
            GroupOrder = 0,
            Order = 55,
            Description = "Local MCP handlers and IPC bridge",
            Card = GetCardAsync
        };
    }

    private static ValueTask<DashboardPageCard> GetCardAsync(HttpContext context)
    {
        var capabilities = context.RequestServices.GetServices<IMcpHandler>()
            .SelectMany(handler => handler.Capabilities ?? [])
            .ToArray();
        var projectOperations = capabilities.Count(capability => string.Equals(capability.Feature, "project", StringComparison.OrdinalIgnoreCase));

        return ValueTask.FromResult(new DashboardPageCard("MCP", "Local MCP operations", capabilities.Length.ToString("N0", CultureInfo.InvariantCulture))
        {
            Detail = projectOperations > 0
                ? $"{projectOperations.ToString("N0", CultureInfo.InvariantCulture)} project operation(s)"
                : "Built-in diagnostics only",
            Icon = "diagram-3",
            Url = McpDashboardEndpoints.BuildMcpPath(context.RequestServices.GetRequiredService<DashboardEndpointsOptions>()),
            Group = "bdk",
            GroupOrder = 0,
            Order = 55
        });
    }
}
