// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Common;
using Spectre.Console;

internal static class JobSchedulerTableBuilders
{
    public static Table BuildJobsTable(IEnumerable<JobSchedulerJobModel> jobs)
    {
        var table = new Table()
            .Border(TableBorder.Minimal)
            .AddColumn("Job")
            .AddColumn("Group")
            .AddColumn("Module")
            .AddColumn("State")
            .AddColumn("Triggers")
            .AddColumn("Pending")
            .AddColumn("Running")
            .AddColumn("Failed")
            .AddColumn("Last execution");

        foreach (var job in jobs.OrderBy(job => job.JobName, StringComparer.OrdinalIgnoreCase))
        {
            table.AddRow(
                Escape(job.JobName),
                Escape(job.Group ?? "-"),
                Escape(job.Module ?? "-"),
                FormatJobState(job),
                job.TriggerCount.ToString(),
                job.PendingOccurrenceCount.ToString(),
                job.RunningOccurrenceCount.ToString(),
                job.FailedOccurrenceCount > 0 ? $"[red]{job.FailedOccurrenceCount}[/]" : "0",
                FormatDate(job.LastExecutionUtc));
        }

        return table;
    }

    public static Table BuildTriggersTable(IEnumerable<JobSchedulerTriggerModel> triggers)
    {
        var table = new Table()
            .Border(TableBorder.Minimal)
            .AddColumn("Job")
            .AddColumn("Trigger")
            .AddColumn("Type")
            .AddColumn("State")
            .AddColumn("Schedule")
            .AddColumn("Next due")
            .AddColumn("Last occurrence");

        foreach (var trigger in triggers.OrderBy(trigger => trigger.JobName, StringComparer.OrdinalIgnoreCase).ThenBy(trigger => trigger.TriggerName, StringComparer.OrdinalIgnoreCase))
        {
            table.AddRow(
                Escape(trigger.JobName),
                Escape(trigger.TriggerName),
                Escape(trigger.TriggerType.ToString()),
                FormatTriggerState(trigger),
                Escape(trigger.Schedule ?? trigger.Delay?.ToString() ?? "-"),
                FormatDate(trigger.NextDueUtc ?? trigger.DueUtc),
                FormatDate(trigger.LastOccurrenceUtc));
        }

        return table;
    }

    public static Table BuildOccurrencesTable(IEnumerable<JobSchedulerOccurrenceModel> occurrences)
    {
        var table = new Table()
            .Border(TableBorder.Minimal)
            .AddColumn("Occurrence")
            .AddColumn("Job")
            .AddColumn("Trigger")
            .AddColumn("Status")
            .AddColumn("Due")
            .AddColumn("Attempt")
            .AddColumn("Latest execution")
            .AddColumn("Lease");

        foreach (var occurrence in occurrences.OrderByDescending(occurrence => occurrence.DueUtc))
        {
            table.AddRow(
                occurrence.OccurrenceId.ToString("D"),
                Escape(occurrence.JobName),
                Escape(occurrence.TriggerName),
                FormatOccurrenceStatus(occurrence.Status),
                FormatDate(occurrence.DueUtc),
                occurrence.AttemptCount.ToString(),
                Escape(occurrence.LatestExecutionStatus?.ToString() ?? "-"),
                Escape(occurrence.LeaseOwnerSchedulerInstanceId ?? "-"));
        }

        return table;
    }

    public static Table BuildHistoryTable(IEnumerable<JobSchedulerExecutionHistoryModel> history)
    {
        var table = new Table()
            .Border(TableBorder.Minimal)
            .AddColumn("Recorded")
            .AddColumn("Job")
            .AddColumn("Trigger")
            .AddColumn("Event")
            .AddColumn("Occurrence")
            .AddColumn("Execution")
            .AddColumn("Message");

        foreach (var entry in history.OrderByDescending(entry => entry.RecordedAt))
        {
            table.AddRow(
                FormatDate(entry.RecordedAt),
                Escape(entry.JobName),
                Escape(entry.TriggerName),
                Escape(entry.EventName),
                Escape(entry.OccurrenceStatus?.ToString() ?? "-"),
                Escape(entry.ExecutionStatus?.ToString() ?? "-"),
                Escape(Shorten(entry.Message, 48)));
        }

        return table;
    }

    public static Table BuildMetricsTable(JobSchedulerMetricsModel metrics)
    {
        var table = new Table()
            .Border(TableBorder.Minimal)
            .AddColumn("Metric")
            .AddColumn("Value");

        table.AddRow("Registered jobs", metrics.RegisteredJobCount.ToString());
        table.AddRow("Registered triggers", metrics.RegisteredTriggerCount.ToString());
        table.AddRow("Occurrences", metrics.OccurrenceCount.ToString());
        table.AddRow("Executions", metrics.ExecutionCount.ToString());
        table.AddRow("Batches", metrics.BatchCount.ToString());
        table.AddRow("Active leases", metrics.ActiveLeaseCount.ToString());
        table.AddRow("Expired leases", metrics.ExpiredLeaseCount > 0 ? $"[yellow]{metrics.ExpiredLeaseCount}[/]" : "0");
        return table;
    }

    private static string FormatJobState(JobSchedulerJobModel job)
    {
        if (!job.EffectiveEnabled)
        {
            return "[grey]Disabled[/]";
        }

        if (job.Paused)
        {
            return "[yellow]Paused[/]";
        }

        if (job.RunningOccurrenceCount > 0)
        {
            return "[green]Running[/]";
        }

        return "[green]Enabled[/]";
    }

    private static string FormatTriggerState(JobSchedulerTriggerModel trigger)
    {
        if (!trigger.EffectiveEnabled)
        {
            return "[grey]Disabled[/]";
        }

        return trigger.Paused ? "[yellow]Paused[/]" : "[green]Enabled[/]";
    }

    private static string FormatOccurrenceStatus(JobOccurrenceStatus status)
        => status switch
        {
            JobOccurrenceStatus.Completed => "[green]Completed[/]",
            JobOccurrenceStatus.Failed => "[red]Failed[/]",
            JobOccurrenceStatus.Cancelled => "[yellow]Cancelled[/]",
            JobOccurrenceStatus.Running => "[green]Running[/]",
            JobOccurrenceStatus.Paused => "[yellow]Paused[/]",
            _ => Escape(status.ToString())
        };

    private static string FormatDate(DateTimeOffset? value)
        => value?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "-";

    private static string Escape(string value)
        => Markup.Escape(string.IsNullOrWhiteSpace(value) ? "-" : value);

    private static string Shorten(string value, int maxLength)
        => string.IsNullOrWhiteSpace(value) || value.Length <= maxLength ? value ?? "-" : value[..(maxLength - 3)] + "...";
}
