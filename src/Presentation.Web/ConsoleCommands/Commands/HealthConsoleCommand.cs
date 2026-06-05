// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Spectre.Console;

/// <summary>
/// Runs registered health checks and prints the report to the interactive console.
/// </summary>
/// <example>
/// <code>
/// health
/// </code>
/// </example>
public class HealthConsoleCommand : ConsoleCommandBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HealthConsoleCommand" /> class.
    /// </summary>
    /// <example>
    /// <code>
    /// health
    /// </code>
    /// </example>
    public HealthConsoleCommand() : base("health", "Run health checks", "hc", "healthz") { }

    /// <inheritdoc />
    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var healthCheckService = services.GetService<HealthCheckService>();
        if (healthCheckService is null)
        {
            console.MarkupLine("[yellow]No health check service registered.[/]");
            return;
        }

        var report = await healthCheckService.CheckHealthAsync();
        var summary = new Table().Border(TableBorder.Minimal);
        summary.AddColumn("Metric");
        summary.AddColumn("Value");
        summary.AddRow("Status", FormatStatus(report.Status));
        summary.AddRow("Total Duration", report.TotalDuration.ToString());
        summary.AddRow("Entries", report.Entries.Count.ToString());
        console.Write(summary);

        if (report.Entries.Count == 0)
        {
            return;
        }

        var entries = new Table().Border(TableBorder.Minimal);
        entries.AddColumn("Name");
        entries.AddColumn("Status");
        entries.AddColumn("Duration");
        entries.AddColumn("Description");
        entries.AddColumn("Exception");

        foreach (var entry in report.Entries.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase))
        {
            entries.AddRow(
                Markup.Escape(entry.Key),
                FormatStatus(entry.Value.Status),
                Markup.Escape(entry.Value.Duration.ToString()),
                Markup.Escape(entry.Value.Description ?? string.Empty),
                Markup.Escape(entry.Value.Exception?.GetBaseException().Message ?? string.Empty));
        }

        console.Write(entries);
    }

    private static string FormatStatus(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => "[green]Healthy[/]",
        HealthStatus.Degraded => "[yellow]Degraded[/]",
        HealthStatus.Unhealthy => "[red]Unhealthy[/]",
        _ => Markup.Escape(status.ToString()),
    };
}
