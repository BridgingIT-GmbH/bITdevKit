// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Application.Jobs;
using Spectre.Console;

public class JobSchedulerListConsoleCommand : JobSchedulerConsoleCommandBase
{
    [ConsoleCommandOption("name", Alias = "n", Description = "Filter by job name")]
    public string JobName { get; set; }

    [ConsoleCommandOption("group", Alias = "g", Description = "Filter by group")]
    public string Group { get; set; }

    [ConsoleCommandOption("module", Alias = "m", Description = "Filter by module")]
    public string Module { get; set; }

    [ConsoleCommandOption("enabled", Description = "Show enabled jobs only")]
    public bool Enabled { get; set; }

    [ConsoleCommandOption("disabled", Description = "Show disabled jobs only")]
    public bool Disabled { get; set; }

    [ConsoleCommandOption("paused", Description = "Show paused jobs only")]
    public bool Paused { get; set; }

    [ConsoleCommandOption("take", Alias = "t", Description = "Max items to show", Default = 50)]
    public int Take { get; set; }

    public JobSchedulerListConsoleCommand()
        : base("list", "List registered jobs") { }

    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        if (this.Enabled && this.Disabled)
        {
            console.MarkupLine("[red]Use either --enabled or --disabled, not both.[/]");
            return;
        }

        var query = this.GetRequired<IJobSchedulerQueryService>(console, services);
        if (query is null)
        {
            return;
        }

        var result = await query.QueryJobsAsync(new JobSchedulerJobQueryRequest
        {
            JobName = this.JobName,
            Group = this.Group,
            Module = this.Module,
            Enabled = this.Enabled ? true : this.Disabled ? false : null,
            Paused = this.Paused ? true : null,
            Take = Math.Max(1, this.Take),
            SortBy = "JobName",
            SortDescending = false
        }).ConfigureAwait(false);

        if (result.IsFailure)
        {
            WritePagedErrors(console, result);
            return;
        }

        var jobs = result.Value?.ToList() ?? [];
        if (jobs.Count == 0)
        {
            console.MarkupLine("[yellow]No jobs found[/]");
            return;
        }

        console.Write(JobSchedulerTableBuilders.BuildJobsTable(jobs));
    }
}
