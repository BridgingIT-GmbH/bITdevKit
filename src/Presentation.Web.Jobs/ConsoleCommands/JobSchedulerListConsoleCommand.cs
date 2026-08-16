// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Application.Jobs;
using Spectre.Console;

/// <summary>
/// Represents job scheduler list console command.
/// </summary>
public class JobSchedulerListConsoleCommand : JobSchedulerConsoleCommandBase
{
    /// <summary>
    /// Gets or sets the job name.
    /// </summary>
    [ConsoleCommandOption("name", Alias = "n", Description = "Filter by job name")]
    public string JobName { get; set; }

    /// <summary>
    /// Gets or sets the group.
    /// </summary>
    [ConsoleCommandOption("group", Alias = "g", Description = "Filter by group")]
    public string Group { get; set; }

    /// <summary>
    /// Gets or sets the module.
    /// </summary>
    [ConsoleCommandOption("module", Alias = "m", Description = "Filter by module")]
    public string Module { get; set; }

    /// <summary>
    /// Gets or sets the enabled.
    /// </summary>
    [ConsoleCommandOption("enabled", Description = "Show enabled jobs only")]
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the disabled.
    /// </summary>
    [ConsoleCommandOption("disabled", Description = "Show disabled jobs only")]
    public bool Disabled { get; set; }

    /// <summary>
    /// Gets or sets the paused.
    /// </summary>
    [ConsoleCommandOption("paused", Description = "Show paused jobs only")]
    public bool Paused { get; set; }

    /// <summary>
    /// Gets or sets the take.
    /// </summary>
    [ConsoleCommandOption("take", Alias = "t", Description = "Max items to show", Default = 50)]
    public int Take { get; set; }

    /// <summary>
    /// Initializes a new instance of the <c>JobSchedulerListConsoleCommand</c> class.
    /// </summary>
    public JobSchedulerListConsoleCommand()
        : base("list", "List registered jobs") { }

    /// <inheritdoc/>
    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
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
