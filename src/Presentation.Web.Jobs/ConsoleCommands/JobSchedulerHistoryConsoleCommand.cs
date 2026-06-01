// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Application.Jobs;
using Spectre.Console;

public class JobSchedulerHistoryConsoleCommand : JobSchedulerConsoleCommandBase
{
    [ConsoleCommandArgument(0, Description = "Job name", Required = false)]
    public string JobName { get; set; }

    [ConsoleCommandOption("trigger", Alias = "r", Description = "Filter by trigger name")]
    public string TriggerName { get; set; }

    [ConsoleCommandOption("occurrence", Alias = "o", Description = "Filter by occurrence id")]
    public Guid OccurrenceId { get; set; }

    [ConsoleCommandOption("event", Alias = "e", Description = "Filter by event name")]
    public string EventName { get; set; }

    [ConsoleCommandOption("from", Description = "Recorded from date/time")]
    public string From { get; set; }

    [ConsoleCommandOption("to", Description = "Recorded to date/time")]
    public string To { get; set; }

    [ConsoleCommandOption("take", Alias = "t", Description = "Max items to show", Default = 50)]
    public int Take { get; set; }

    public JobSchedulerHistoryConsoleCommand()
        : base("history", "Show retained job execution history", "hist") { }

    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
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
