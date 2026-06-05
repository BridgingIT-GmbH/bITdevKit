// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Logging.Dashboard;

using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

/// <summary>
/// Maps dashboard pages for presentation endpoint diagnostics.
/// </summary>
/// <example>
/// <code>
/// services.AddDashboard(options => options.WithPluginAssemblyContaining&lt;DashboardEndpoints&gt;());
/// </code>
/// </example>
public sealed class DashboardEndpoints(DashboardEndpointsOptions options) : EndpointsBase, IDashboardEndpoints
{
    private const string LogEntriesPath = "/logentries";
    private const string LogEntriesContentPath = "/logentries/content";
    private const string LogEntriesRowsPath = "/logentries/rows";
    private const string LogEntriesStreamPath = "/logentries/stream";
    private const string LogEntriesStreamHubPath = "/logentries/stream/hub";

    /// <inheritdoc />
    public override void Map(IEndpointRouteBuilder app)
    {
        options ??= new DashboardEndpointsOptions();

        if (!options.Enabled)
        {
            return;
        }

        var group = this.MapGroup(app, options)
            .WithTags("_bdk.Dashboard");

        group.MapDashboardPage<Pages.Index>(
            LogEntriesPath,
            "_bdk.Dashboard.LogEntries",
            "Dashboard Log Entries",
            "Shows stored log entries from the registered log entry service.");

        group.MapDashboardPage<Pages.Content>(
            LogEntriesContentPath,
            "_bdk.Dashboard.LogEntriesContent",
            "Dashboard Log Entries Content",
            "Shows the dashboard log entries content fragment.");

        group.MapDashboardPage<Pages.Rows>(
            LogEntriesRowsPath,
            "_bdk.Dashboard.LogEntriesRows",
            "Dashboard Log Entries Rows",
            "Shows dashboard log entry rows.");

        group.MapDashboardPage<Pages.Stream>(
            LogEntriesStreamPath,
            "_bdk.Dashboard.LogEntriesStream",
            "Dashboard Log Entries Stream",
            "Streams stored log entries to a dashboard terminal view.");

        group.MapHub<WebLogStreamHub>(LogEntriesStreamHubPath)
            .WithSummary("Dashboard Log Entries Stream Hub")
            .WithDescription("Streams stored log entries to the dashboard log stream terminal.");
    }

    internal static string BuildLogEntriesPath(DashboardEndpointsOptions options)
    {
        return DashboardPath.Combine(options?.GroupPath, LogEntriesPath);
    }

    internal static string BuildLogEntriesContentPath(DashboardEndpointsOptions options)
    {
        return DashboardPath.Combine(options?.GroupPath, LogEntriesContentPath);
    }

    internal static string BuildLogEntriesRowsPath(DashboardEndpointsOptions options)
    {
        return DashboardPath.Combine(options?.GroupPath, LogEntriesRowsPath);
    }

    internal static string BuildLogEntriesStreamPath(DashboardEndpointsOptions options)
    {
        return DashboardPath.Combine(options?.GroupPath, LogEntriesStreamPath);
    }

    internal static string BuildLogEntriesStreamHubPath(DashboardEndpointsOptions options)
    {
        return DashboardPath.Combine(options?.GroupPath, LogEntriesStreamHubPath);
    }
}
