// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Application.Orchestrations;
using Spectre.Console;

public class OrchestrationListConsoleCommand : OrchestrationConsoleCommandBase
{
    [ConsoleCommandOption("name", Alias = "n", Description = "Filter by orchestration name")]
    public string OrchestrationName { get; set; }

    [ConsoleCommandOption("status", Alias = "s", Description = "Comma-separated lifecycle status filter")]
    public string Status { get; set; }

    [ConsoleCommandOption("state", Description = "Comma-separated current state filter")]
    public string State { get; set; }

    [ConsoleCommandOption("correlation", Alias = "c", Description = "Filter by correlation id")]
    public string CorrelationId { get; set; }

    [ConsoleCommandOption("from", Description = "Started from date/time")]
    public string From { get; set; }

    [ConsoleCommandOption("to", Description = "Started to date/time")]
    public string To { get; set; }

    [ConsoleCommandOption("take", Alias = "t", Description = "Max items to show", Default = 50)]
    public int Take { get; set; }

    public OrchestrationListConsoleCommand()
        : base("list", "List orchestration instances") { }

    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        if (!TryParseDate(this.From, out var from) || !TryParseDate(this.To, out var to))
        {
            console.MarkupLine("[red]Invalid date. Use an ISO date/time or yyyy-MM-dd.[/]");
            return;
        }

        var query = this.GetRequired<IOrchestrationQueryService>(console, services);
        if (query is null)
        {
            return;
        }

        var result = await query.QueryAsync(new OrchestrationQueryRequest
        {
            OrchestrationName = this.OrchestrationName,
            Statuses = SplitCsv(this.Status),
            States = SplitCsv(this.State),
            CorrelationId = this.CorrelationId,
            StartedFrom = from,
            StartedTo = to,
            Take = Math.Max(1, this.Take),
            SortBy = "StartedUtc",
            SortDescending = true
        }).ConfigureAwait(false);

        if (result.IsFailure)
        {
            WritePagedErrors(console, result);
            return;
        }

        var instances = result.Value?.ToList() ?? [];
        if (instances.Count == 0)
        {
            console.MarkupLine("[yellow]No orchestration instances found[/]");
            return;
        }

        console.Write(OrchestrationTableBuilders.BuildInstancesTable(instances));
    }
}
