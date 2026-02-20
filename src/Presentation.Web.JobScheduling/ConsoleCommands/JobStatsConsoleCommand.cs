// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Spectre.Console;
using System;

public class JobStatsConsoleCommand : JobGroupConsoleCommandBase
{
    [ConsoleCommandArgument(0, Description = "Job name", Required = true)]
    public string JobName { get; set; }

    [ConsoleCommandArgument(1, Description = "Job group", Required = false)]
    public string JobGroup { get; set; }

    [ConsoleCommandOption("from", Description = "Start date (yyyy-MM-dd)")]
    public string From { get; set; }

    [ConsoleCommandOption("to", Description = "End date (yyyy-MM-dd)")]
    public string To { get; set; }

    public JobStatsConsoleCommand() : base("stats", "Show job statistics") { }

    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        if (string.IsNullOrWhiteSpace(this.JobName))
        {
            console.MarkupLine("[red]Job name is required[/]");

            return;
        }

        await this.ExecuteWithJobServiceAsync(console, services, async jobService =>
        {
            var jobGroup = this.NormalizeJobGroup(this.JobGroup);

            DateTimeOffset? startDate = null;
            DateTimeOffset? endDate = null;

            if (!string.IsNullOrWhiteSpace(this.From) && DateTimeOffset.TryParse(this.From, out var fromDate))
            {
                startDate = fromDate;
            }

            if (!string.IsNullOrWhiteSpace(this.To) && DateTimeOffset.TryParse(this.To, out var toDate))
            {
                endDate = toDate;
            }

            var job = await jobService.GetJobAsync(this.JobName, jobGroup);
            if (job == null)
            {
                console.MarkupLine($"[yellow]Job '{this.JobName}' not found[/]");
                return;
            }

            var stats = await jobService.GetJobRunStatsAsync(this.JobName, jobGroup, startDate, endDate);
            if (stats == null)
            {
                console.MarkupLine($"[yellow]No statistics available for '{this.JobName}'[/]");
                return;
            }

            var table = JobTableBuilders.BuildJobStatsTable(stats, job);
            console.Write(table);
        });
    }
}
