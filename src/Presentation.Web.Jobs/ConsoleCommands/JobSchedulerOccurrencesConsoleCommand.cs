// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Common;
using Spectre.Console;

public class JobSchedulerOccurrencesConsoleCommand : JobSchedulerConsoleCommandBase
{
    [ConsoleCommandArgument(0, Description = "Job name", Required = false)]
    public string JobName { get; set; }

    [ConsoleCommandOption("trigger", Alias = "r", Description = "Filter by trigger name")]
    public string TriggerName { get; set; }

    [ConsoleCommandOption("status", Alias = "s", Description = "Filter by occurrence status")]
    public string Status { get; set; }

    [ConsoleCommandOption("from", Description = "Due from date/time")]
    public string From { get; set; }

    [ConsoleCommandOption("to", Description = "Due to date/time")]
    public string To { get; set; }

    [ConsoleCommandOption("take", Alias = "t", Description = "Max items to show", Default = 50)]
    public int Take { get; set; }

    public JobSchedulerOccurrencesConsoleCommand()
        : base("occurrences", "List materialized job occurrences", "occ") { }

    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        if (!TryParseDate(this.From, out var from) || !TryParseDate(this.To, out var to))
        {
            console.MarkupLine("[red]Invalid date. Use an ISO date/time or yyyy-MM-dd.[/]");
            return;
        }

        if (!TryParseEnumList<JobOccurrenceStatus>(this.Status, out var statuses))
        {
            console.MarkupLine($"[red]Unknown occurrence status '{Markup.Escape(this.Status)}'[/]");
            return;
        }

        var query = this.GetRequired<IJobSchedulerQueryService>(console, services);
        if (query is null)
        {
            return;
        }

        var result = await query.QueryOccurrencesAsync(new JobSchedulerOccurrenceQueryRequest
        {
            JobName = this.JobName,
            TriggerName = this.TriggerName,
            Statuses = statuses,
            DueFrom = from,
            DueTo = to,
            Take = Math.Max(1, this.Take),
            SortBy = "DueUtc",
            SortDescending = true
        }).ConfigureAwait(false);

        if (result.IsFailure)
        {
            WritePagedErrors(console, result);
            return;
        }

        var occurrences = result.Value?.ToList() ?? [];
        if (occurrences.Count == 0)
        {
            console.MarkupLine("[yellow]No occurrences found[/]");
            return;
        }

        console.Write(JobSchedulerTableBuilders.BuildOccurrencesTable(occurrences));
    }

    private static bool TryParseEnumList<T>(string value, out IReadOnlyList<T> values)
        where T : struct
    {
        values = [];
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var result = new List<T>();
        foreach (var item in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Enum.TryParse<T>(item, true, out var parsed))
            {
                return false;
            }

            result.Add(parsed);
        }

        values = result;
        return true;
    }
}
