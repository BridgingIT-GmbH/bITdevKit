// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Application.Orchestrations;
using Spectre.Console;

public class OrchestrationHistoryConsoleCommand : OrchestrationConsoleCommandBase
{
    [ConsoleCommandArgument(0, Description = "Orchestration instance id", Required = true)]
    public Guid InstanceId { get; set; }

    public OrchestrationHistoryConsoleCommand()
        : base("history", "Show orchestration instance history", "hist") { }

    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var query = this.GetRequired<IOrchestrationQueryService>(console, services);
        if (query is null)
        {
            return;
        }

        var result = await query.GetHistoryAsync(this.InstanceId).ConfigureAwait(false);
        if (result.IsFailure)
        {
            WriteErrors(console, result);
            return;
        }

        var history = result.Value?.ToList() ?? [];
        if (history.Count == 0)
        {
            console.MarkupLine("[yellow]No history found[/]");
            return;
        }

        console.Write(OrchestrationTableBuilders.BuildHistoryTable(history));
    }
}
