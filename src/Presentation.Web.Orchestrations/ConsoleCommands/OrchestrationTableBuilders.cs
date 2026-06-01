// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Application.Orchestrations;
using Spectre.Console;

internal static class OrchestrationTableBuilders
{
    public static Table BuildInstancesTable(IEnumerable<OrchestrationInstanceModel> instances)
    {
        var table = new Table()
            .Border(TableBorder.Minimal)
            .AddColumn("Instance")
            .AddColumn("Orchestration")
            .AddColumn("Status")
            .AddColumn("State")
            .AddColumn("Activity")
            .AddColumn("Started")
            .AddColumn("Updated");

        foreach (var instance in instances.OrderByDescending(instance => instance.LastUpdatedUtc))
        {
            table.AddRow(
                instance.InstanceId.ToString("D"),
                Escape(instance.OrchestrationName),
                FormatStatus(instance.Status),
                Escape(instance.CurrentState),
                Escape(instance.CurrentActivity),
                FormatDate(instance.StartedUtc),
                FormatDate(instance.LastUpdatedUtc));
        }

        return table;
    }

    public static Table BuildHistoryTable(IEnumerable<OrchestrationHistoryModel> history)
    {
        var table = new Table()
            .Border(TableBorder.Minimal)
            .AddColumn("Timestamp")
            .AddColumn("Event")
            .AddColumn("State")
            .AddColumn("Activity")
            .AddColumn("Message");

        foreach (var entry in history.OrderByDescending(entry => entry.TimestampUtc))
        {
            table.AddRow(
                FormatDate(entry.TimestampUtc),
                Escape(entry.EventType),
                Escape(entry.State),
                Escape(entry.Activity),
                Escape(Shorten(entry.Message, 72)));
        }

        return table;
    }

    public static Table BuildMetricsTable(OrchestrationMetricsModel metrics)
    {
        var table = new Table()
            .Border(TableBorder.Minimal)
            .AddColumn("Metric")
            .AddColumn("Value");

        table.AddRow("Total", metrics.TotalCount.ToString());
        table.AddRow("Running", metrics.RunningCount.ToString());
        table.AddRow("Waiting", metrics.WaitingCount.ToString());
        table.AddRow("Paused", metrics.PausedCount.ToString());
        table.AddRow("Completed", metrics.CompletedCount.ToString());
        table.AddRow("Failed", metrics.FailedCount > 0 ? $"[red]{metrics.FailedCount}[/]" : "0");
        table.AddRow("Cancelled", metrics.CancelledCount.ToString());
        table.AddRow("Terminated", metrics.TerminatedCount.ToString());
        table.AddRow("Average duration", metrics.AverageDurationSeconds.HasValue ? $"{metrics.AverageDurationSeconds.Value:F2}s" : "-");
        table.AddRow("Oldest waiting", FormatDate(metrics.OldestWaitingStartedUtc));
        return table;
    }

    public static Table BuildDefinitionTable(IEnumerable<string> definitions)
    {
        var table = new Table()
            .Border(TableBorder.Minimal)
            .AddColumn("Definition");

        foreach (var definition in definitions.OrderBy(definition => definition, StringComparer.OrdinalIgnoreCase))
        {
            table.AddRow(Escape(definition));
        }

        return table;
    }

    private static string FormatStatus(string status)
        => status?.ToUpperInvariant() switch
        {
            "COMPLETED" => "[green]Completed[/]",
            "FAILED" => "[red]Failed[/]",
            "PAUSED" => "[yellow]Paused[/]",
            "CANCELLED" => "[yellow]Cancelled[/]",
            "TERMINATED" => "[red]Terminated[/]",
            "RUNNING" => "[green]Running[/]",
            "WAITING" => "[yellow]Waiting[/]",
            _ => Escape(status)
        };

    private static string FormatDate(DateTimeOffset? value)
        => value?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "-";

    private static string Escape(string value)
        => Markup.Escape(string.IsNullOrWhiteSpace(value) ? "-" : value);

    private static string Shorten(string value, int maxLength)
        => string.IsNullOrWhiteSpace(value) || value.Length <= maxLength ? value ?? "-" : value[..(maxLength - 3)] + "...";
}
