// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Spectre.Console;
using System;

public class JobListConsoleCommand : JobGroupConsoleCommandBase
{
    [ConsoleCommandArgument(0, Description = "Job group name", Required = false)]
    public string JobGroup { get; set; }

    [ConsoleCommandOption("filter", Alias = "f", Description = "Filter jobs by name")]
    public string Filter { get; set; }

    [ConsoleCommandOption("status", Alias = "s", Description = "Filter by status (Running/Idle/Failed)")]
    public string Status { get; set; }

    public JobListConsoleCommand() : base("list", "List all jobs in group") { }

    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        await this.ExecuteWithJobServiceAsync(console, services, async jobService =>
        {
            var jobGroup = this.NormalizeJobGroup(this.JobGroup);
            var jobs = await jobService.GetJobsAsync();
            if (jobs?.Any() != true)
            {
                console.MarkupLine($"[yellow]No jobs found in group '{jobGroup}'[/]");
                return;
            }

            // Filter by group
            jobs = [.. jobs.Where(j => j.Group?.Equals(jobGroup, StringComparison.OrdinalIgnoreCase) ?? false)];

            if (!jobs.Any())
            {
                console.MarkupLine($"[yellow]No jobs found in group '{jobGroup}'[/]");
                return;
            }

            // Apply name filter
            if (!string.IsNullOrWhiteSpace(this.Filter))
            {
                jobs = [.. jobs.Where(j => j.Name.Contains(this.Filter, StringComparison.OrdinalIgnoreCase))];
            }

            // Apply status filter
            if (!string.IsNullOrWhiteSpace(this.Status))
            {
                jobs = [.. jobs.Where(j =>
                {
                    if (this.Status.Equals("Running", StringComparison.OrdinalIgnoreCase)) { return j.IsRunning; } if (this.Status.Equals("Idle", StringComparison.OrdinalIgnoreCase)) { return !j.IsRunning && j.Status == "Idle"; } if (this.Status.Equals("Failed", StringComparison.OrdinalIgnoreCase)) { return j.Status == "Failed"; } return false;
                })];
            }

            if (!jobs.Any())
            {
                console.MarkupLine("[yellow]No jobs match the filter criteria[/]");
                return;
            }

            var table = JobTableBuilders.BuildJobsTable(jobs);
            console.Write(table);
        });
    }
}
