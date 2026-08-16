// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Application.Jobs;
using Spectre.Console;

/// <summary>
/// Represents job scheduler history console command.
/// </summary>
public class JobSchedulerHistoryConsoleCommand : JobSchedulerConsoleCommandBase
{
    /// <summary>
    /// Gets or sets the job name.
    /// </summary>
    [ConsoleCommandArgument(0, Description = "Job name", Required = false)]
    public string JobName { get; set; }

    /// <summary>
    /// Gets or sets the trigger name.
    /// </summary>
    [ConsoleCommandOption("trigger", Alias = "r", Description = "Filter by trigger name")]
    public string TriggerName { get; set; }

    /// <summary>
    /// Gets or sets the occurrence id.
    /// </summary>
    [ConsoleCommandOption("occurrence", Alias = "o", Description = "Filter by occurrence id")]
    public Guid OccurrenceId { get; set; }

    /// <summary>
    /// Gets or sets the event name.
    /// </summary>
    [ConsoleCommandOption("event", Alias = "e", Description = "Filter by event name")]
    public string EventName { get; set; }

    /// <summary>
    /// Gets or sets the from.
    /// </summary>
    [ConsoleCommandOption("from", Description = "Recorded from date/time")]
    public string From { get; set; }

    /// <summary>
    /// Gets or sets the to.
    /// </summary>
    [ConsoleCommandOption("to", Description = "Recorded to date/time")]
    public string To { get; set; }

    /// <summary>
    /// Gets or sets the take.
    /// </summary>
    [ConsoleCommandOption("take", Alias = "t", Description = "Max items to show", Default = 50)]
    public int Take { get; set; }

    /// <summary>
    /// Initializes a new instance of the <c>JobSchedulerHistoryConsoleCommand</c> class.
    /// </summary>
    public JobSchedulerHistoryConsoleCommand()
        : base("history", "Show retained job execution history", "hist") { }

    /// <inheritdoc/>
    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        if (!TryParseDate(this.From, out var from) || !TryParseDate(this.To, out var to))
        {
            console.MarkupLine("[red]Invalid date. Use an ISO date/time or yyyy-MM-dd.[/]");
            return;
        }

        var query = this.GetRequired<IJobSchedulerQueryService>(console, services);
        if (query is null)
        {
            return;
        }

        var result = await query.QueryExecutionHistoryAsync(new JobSchedulerExecutionHistoryQueryRequest
        {
            JobName = this.JobName,
            TriggerName = this.TriggerName,
            OccurrenceId = this.OccurrenceId == Guid.Empty ? null : this.OccurrenceId,
            EventNames = string.IsNullOrWhiteSpace(this.EventName) ? [] : [this.EventName],
            RecordedFromUtc = from,
            RecordedToUtc = to,
            Take = Math.Max(1, this.Take),
            SortBy = "RecordedAt",
            SortDescending = true
        }).ConfigureAwait(false);

        if (result.IsFailure)
        {
            WritePagedErrors(console, result);
            return;
        }

        var history = result.Value?.ToList() ?? [];
        if (history.Count == 0)
        {
            console.MarkupLine("[yellow]No history found[/]");
            return;
        }

        console.Write(JobSchedulerTableBuilders.BuildHistoryTable(history));
    }
}
