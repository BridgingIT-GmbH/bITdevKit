// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Logging.Dashboard;

using System.Globalization;
using System.Net;
using System.Text;
using BridgingIT.DevKit.Application.Utilities;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

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
    private const string LogEntriesExportPath = "/logentries/export";
    private const string LogEntriesPurgePath = "/logentries/purge";
    private const string ErrorsPath = "/errors";
    private const string ErrorsContentPath = "/errors/content";
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

        group.MapGet(LogEntriesExportPath, (Delegate)(async (HttpContext httpContext) => await this.ExportLogEntries(httpContext)))
            .Produces((int)HttpStatusCode.OK, contentType: "text/plain")
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Dashboard.LogEntries.Export")
            .WithSummary("Export dashboard log entries")
            .WithDescription("Exports dashboard log entries as a text file using the current log entries filters.");

        group.MapPost(LogEntriesPurgePath, (Delegate)(async (HttpContext httpContext) =>
            await PurgeLogEntries(httpContext)))
            .Produces((int)HttpStatusCode.Accepted)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Dashboard.LogEntries.Purge")
            .WithSummary("Purge dashboard log entries")
            .WithDescription("Queues cleanup for retained log entries.");

        group.MapDashboardPage<Pages.Errors>(
            ErrorsPath,
            "_bdk.Dashboard.Errors",
            "Dashboard Errors",
            "Shows stored error log entries from the registered log entry service.");

        group.MapDashboardPage<Pages.ErrorsContent>(
            ErrorsContentPath,
            "_bdk.Dashboard.ErrorsContent",
            "Dashboard Errors Content",
            "Shows the dashboard errors content fragment.");

        group.MapDashboardPage<Pages.Stream>(
            LogEntriesStreamPath,
            "_bdk.Dashboard.LogEntriesStream",
            "Dashboard Log Entries Stream",
            "Streams stored log entries to a dashboard terminal view.");

        group.MapHub<WebLogStreamHub>(LogEntriesStreamHubPath)
            .WithSummary("Dashboard Log Entries Stream Hub")
            .WithDescription("Streams stored log entries to the dashboard log stream terminal.");
    }

    /// <summary>
    /// Builds the dashboard log entries page path.
    /// </summary>
    /// <param name="options">The dashboard endpoint options.</param>
    /// <returns>The absolute dashboard path for the log entries page.</returns>
    /// <example>
    /// <code>
    /// var path = DashboardEndpoints.BuildLogEntriesPath(options);
    /// </code>
    /// </example>
    public static string BuildLogEntriesPath(DashboardEndpointsOptions options)
    {
        return DashboardPath.Combine(options?.GroupPath, LogEntriesPath);
    }

    /// <summary>
    /// Builds the dashboard log entries content fragment path.
    /// </summary>
    /// <param name="options">The dashboard endpoint options.</param>
    /// <returns>The absolute dashboard path for the log entries content fragment.</returns>
    /// <example>
    /// <code>
    /// var path = DashboardEndpoints.BuildLogEntriesContentPath(options);
    /// </code>
    /// </example>
    public static string BuildLogEntriesContentPath(DashboardEndpointsOptions options)
    {
        return DashboardPath.Combine(options?.GroupPath, LogEntriesContentPath);
    }

    /// <summary>
    /// Builds the dashboard log entries rows fragment path.
    /// </summary>
    /// <param name="options">The dashboard endpoint options.</param>
    /// <returns>The absolute dashboard path for the log entries rows fragment.</returns>
    /// <example>
    /// <code>
    /// var path = DashboardEndpoints.BuildLogEntriesRowsPath(options);
    /// </code>
    /// </example>
    public static string BuildLogEntriesRowsPath(DashboardEndpointsOptions options)
    {
        return DashboardPath.Combine(options?.GroupPath, LogEntriesRowsPath);
    }

    /// <summary>
    /// Builds the dashboard log entries text export path.
    /// </summary>
    /// <param name="options">The dashboard endpoint options.</param>
    /// <returns>The absolute dashboard path for the log entries text export endpoint.</returns>
    /// <example>
    /// <code>
    /// var path = DashboardEndpoints.BuildLogEntriesExportPath(options);
    /// </code>
    /// </example>
    public static string BuildLogEntriesExportPath(DashboardEndpointsOptions options)
    {
        return DashboardPath.Combine(options?.GroupPath, LogEntriesExportPath);
    }

    /// <summary>
    /// Builds the dashboard log entries purge action path.
    /// </summary>
    /// <param name="options">The dashboard endpoint options.</param>
    /// <returns>The absolute dashboard path for the log entries purge action.</returns>
    /// <example>
    /// <code>
    /// var path = DashboardEndpoints.BuildLogEntriesPurgePath(options);
    /// </code>
    /// </example>
    public static string BuildLogEntriesPurgePath(DashboardEndpointsOptions options)
    {
        return DashboardPath.Combine(options?.GroupPath, LogEntriesPurgePath);
    }

    /// <summary>
    /// Builds the dashboard errors page path.
    /// </summary>
    /// <param name="options">The dashboard endpoint options.</param>
    /// <returns>The absolute dashboard path for the errors page.</returns>
    /// <example>
    /// <code>
    /// var path = DashboardEndpoints.BuildErrorsPath(options);
    /// </code>
    /// </example>
    public static string BuildErrorsPath(DashboardEndpointsOptions options)
    {
        return DashboardPath.Combine(options?.GroupPath, ErrorsPath);
    }

    /// <summary>
    /// Builds the dashboard errors content fragment path.
    /// </summary>
    /// <param name="options">The dashboard endpoint options.</param>
    /// <returns>The absolute dashboard path for the errors content fragment.</returns>
    /// <example>
    /// <code>
    /// var path = DashboardEndpoints.BuildErrorsContentPath(options);
    /// </code>
    /// </example>
    public static string BuildErrorsContentPath(DashboardEndpointsOptions options)
    {
        return DashboardPath.Combine(options?.GroupPath, ErrorsContentPath);
    }

    /// <summary>
    /// Builds the dashboard log entries stream page path.
    /// </summary>
    /// <param name="options">The dashboard endpoint options.</param>
    /// <returns>The absolute dashboard path for the log entries stream page.</returns>
    /// <example>
    /// <code>
    /// var path = DashboardEndpoints.BuildLogEntriesStreamPath(options);
    /// </code>
    /// </example>
    public static string BuildLogEntriesStreamPath(DashboardEndpointsOptions options)
    {
        return DashboardPath.Combine(options?.GroupPath, LogEntriesStreamPath);
    }

    /// <summary>
    /// Builds the dashboard log entries stream hub path.
    /// </summary>
    /// <param name="options">The dashboard endpoint options.</param>
    /// <returns>The absolute dashboard path for the log entries stream hub.</returns>
    /// <example>
    /// <code>
    /// var path = DashboardEndpoints.BuildLogEntriesStreamHubPath(options);
    /// </code>
    /// </example>
    public static string BuildLogEntriesStreamHubPath(DashboardEndpointsOptions options)
    {
        return DashboardPath.Combine(options?.GroupPath, LogEntriesStreamHubPath);
    }

    private async Task<IResult> ExportLogEntries(HttpContext httpContext)
    {
        var service = httpContext.RequestServices.GetService<ILogEntryService>();
        if (service is null)
        {
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = "Export Failed",
                Detail = "ILogEntryService is not registered."
            });
        }

        var filter = LogEntriesDashboard.CreateFilter(httpContext);
        var request = LogEntriesDashboard.CreateRequest(filter);
        request.PageSize = 1000;
        request.ContinuationToken = null;
        request.AfterId = null;

        var stream = new MemoryStream();
        await using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);

        string continuationToken = null;
        do
        {
            request.ContinuationToken = continuationToken;

            var response = await service.QueryAsync(request, httpContext.RequestAborted);
            foreach (var entry in response.Items)
            {
                await writer.WriteAsync(FormatLogEntry(entry));
            }

            continuationToken = response.ContinuationToken;
        } while (!string.IsNullOrWhiteSpace(continuationToken));

        await writer.FlushAsync(httpContext.RequestAborted);
        stream.Position = 0;

        var fileName = $"logs-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.txt";
        return Results.File(stream, "text/plain; charset=utf-8", fileName);
    }

    private static async Task<IResult> PurgeLogEntries(HttpContext httpContext)
    {
        var service = httpContext.RequestServices.GetService<ILogEntryService>();
        if (service is null)
        {
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = "Purge Failed",
                Detail = "ILogEntryService is not registered."
            });
        }

        var olderThan = DateTimeOffset.UtcNow.AddDays(1);
        await service.CleanupAsync(olderThan, archive: true, batchSize: 10000, delayInterval: TimeSpan.Zero, cancellationToken: httpContext.RequestAborted);
        await service.CleanupAsync(olderThan, archive: false, batchSize: 10000, delayInterval: TimeSpan.Zero, cancellationToken: httpContext.RequestAborted);

        return Results.Accepted();
    }

    private static string FormatLogEntry(LogEntryModel entry)
    {
        var builder = new StringBuilder();

        builder.Append(entry.TimeStamp.LocalDateTime.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture));
        builder.Append(' ');
        builder.Append(FormatLevel(entry.Level));
        builder.Append(" | cid:");
        builder.Append(entry.CorrelationId);
        builder.Append(" | mod:");
        builder.Append(entry.ModuleName);
        builder.Append(" - ");
        builder.Append(entry.Message);
        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(entry.Exception))
        {
            builder.AppendLine(entry.Exception);
        }

        return builder.ToString();
    }

    private static string FormatLevel(string level)
    {
        return level switch
        {
            "Critical" or "Fatal" => "FTL",
            "Error" => "ERR",
            "Warning" => "WRN",
            "Information" => "INF",
            "Debug" => "DBG",
            "Trace" or "Verbose" => "VRB",
            _ => string.IsNullOrWhiteSpace(level) ? "---" : level[..Math.Min(level.Length, 3)].ToUpperInvariant()
        };
    }
}
