// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Application.JobScheduling;
using Spectre.Console;
using System.Collections.Generic;

public static class JobTableBuilders
{
    public static Table BuildJobsTable(IEnumerable<JobInfo> jobs)
    {
        var table = new Table()
            .Border(TableBorder.Minimal)
            //.Title("[bold cyan]Jobs[/]")
            .AddColumn("Name")
            .AddColumn("Status")
            .AddColumn("Last Run")
            .AddColumn("Duration")
            .AddColumn("Triggers")
            .AddColumn("Cron")
            .AddColumn("Description");

        foreach (var job in jobs.OrderBy(j => j.Name))
        {
            var status = job.IsRunning
                ? "[green]Running[/]"
                : $"[grey]{job.Status ?? "Idle"}[/]";

            var lastRun = job.LastRun?.StartTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
            var duration = job.LastRun?.DurationText ?? "-";
            var cronExpression = GetCronExpression(job.Triggers);
            var desc = (job.Description ?? "").Length > 40
                ? (job.Description ?? "").Substring(0, 37) + "..."
                : job.Description ?? "-";

            table.AddRow(
                job.Name,
                status,
                lastRun,
                duration,
                job.TriggerCount.ToString(),
                cronExpression,
                desc);
        }

        return table;
    }

    public static Table BuildJobRunsTable(IEnumerable<JobRun> runs)
    {
        var table = new Table()
            .Border(TableBorder.Minimal)
            //.Title("[bold cyan]Job Runs[/]")
            .AddColumn("Start Time")
            .AddColumn("Status")
            .AddColumn("Duration")
            .AddColumn("Trigger")
            .AddColumn("Result");

        foreach (var run in runs.OrderByDescending(r => r.StartTime).Take(50))
        {
            var statusIcon = run.Status switch
            {
                "Success" => "[green]V[/]",
                "Completed" => "[green]V[/]",
                "Failed" => "[red]X[/]",
                "Started" => "[yellow]*[/]",
                "Interrupted" => "[orange3]-[/]",
                _ => "[grey]-[/]"
            };

            var result = (run.Result ?? "").Length > 30
                ? (run.Result ?? "").Substring(0, 27) + "..."
                : run.Result ?? "-";

            if (!string.IsNullOrEmpty(run.ErrorMessage))
            {
                result = $"[red]{run.ErrorMessage}[/]";
            }

            table.AddRow(
                run.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                $"{statusIcon} {run.Status}",
                run.DurationText ?? "-",
                run.TriggerName ?? "-",
                result);
        }

        return table;
    }

    public static Table BuildJobStatsTable(JobRunStats stats, JobInfo job)
    {
        var table = new Table()
            .Border(TableBorder.Minimal)
            //.Title($"[bold cyan]Job Stats: {job.Name}[/]")
            .AddColumn("Metric")
            .AddColumn("Value");

        var successRate = stats.TotalRuns > 0
            ? ((stats.SuccessCount / (double)stats.TotalRuns) * 100).ToString("F1") + "%"
            : "N/A";

        table.AddRow("Total Runs", stats.TotalRuns.ToString());
        table.AddRow("Successful", $"[green]{stats.SuccessCount}[/]");
        table.AddRow("Failed", $"[red]{stats.FailureCount}[/]");
        table.AddRow("Interrupted", $"[orange3]{stats.InterruptCount}[/]");
        table.AddRow("Success Rate", successRate);
        table.AddRow("Avg Duration", stats.AvgRunDurationText);
        table.AddRow("Min Duration", stats.MinRunDurationText);
        table.AddRow("Max Duration", stats.MaxRunDurationText);

        return table;
    }

    /// <summary>
    /// Extracts and formats cron expressions from job triggers.
    /// </summary>
    private static string GetCronExpression(IEnumerable<TriggerInfo> triggers)
    {
        if (triggers?.Any() != true)
        {
            return "-";
        }

        var cronExpressions = triggers
            .Where(t => !string.IsNullOrWhiteSpace(t.CronExpression))
            .Select(t => t.CronExpression)
            .Distinct().ToList();

        if (cronExpressions.Count == 0)
        {
            return "-";
        }

        if (cronExpressions.Count == 1)
        {
            return cronExpressions[0];
        }

        // Multiple cron expressions - show first and indicate more
        var first = cronExpressions[0];
        if (first.Length > 35)
        {
            first = first.Substring(0, 32) + "...";
        }

        return cronExpressions.Count > 1
            ? $"{first} [grey](+{cronExpressions.Count - 1} more)[/]"
            : first;
    }
}