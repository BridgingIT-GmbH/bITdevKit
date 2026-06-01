// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Application.Orchestrations;
using Spectre.Console;

public class OrchestrationMetricsConsoleCommand : OrchestrationConsoleCommandBase
{
    [ConsoleCommandOption("name", Alias = "n", Description = "Filter by orchestration name")]
    public string OrchestrationName { get; set; }

    [ConsoleCommandOption("status", Alias = "s", Description = "Comma-separated lifecycle status filter")]
    public string Status { get; set; }

    [ConsoleCommandOption("state", Description = "Comma-separated current state filter")]
    public string State { get; set; }

    public OrchestrationMetricsConsoleCommand()
        : base("metrics", "Show aggregate orchestration metrics", "stats") { }

    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        var query = this.GetRequired<IOrchestrationQueryService>(console, services);
        if (query is null)
        {
            return;
        }

        var result = await query.GetMetricsAsync(new OrchestrationMetricsRequest
        {
            OrchestrationName = this.OrchestrationName,
            Statuses = SplitCsv(this.Status),
            States = SplitCsv(this.State)
        }).ConfigureAwait(false);

        if (result.IsFailure)
        {
            WriteErrors(console, result);
            return;
        }

        console.Write(OrchestrationTableBuilders.BuildMetricsTable(result.Value));
    }
}
