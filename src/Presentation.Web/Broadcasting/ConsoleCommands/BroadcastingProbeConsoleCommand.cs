// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using System.Globalization;
using BridgingIT.DevKit.Common;
using Spectre.Console;

internal sealed class BroadcastingProbeConsoleCommand()
    : BroadcastingConsoleCommandBase(
        "probe",
        "Publish a delivery probe to the default or selected scope",
        "test"
    )
{
    [ConsoleCommandOption(
        "scope",
        Alias = "s",
        Description = "Target scope; defaults to the Broadcasting default scope"
    )]
    public string Scope { get; set; }

    public override async Task ExecuteAsync(
        IAnsiConsole console,
        IServiceProvider services,
        CancellationToken cancellationToken = default
    )
    {
        var service = GetRequired<IBroadcastService>(console, services);
        if (service is null)
        {
            return;
        }

        IEnumerable<string> scopes = string.IsNullOrWhiteSpace(this.Scope)
            ? null
            : [this.Scope.Trim()];
        var result = await service
            .PublishAsync(
                new BroadcastProbe(Guid.NewGuid(), DateTimeOffset.UtcNow),
                scopes,
                new BroadcastPublishOptions { RequireAtLeastOneTarget = true },
                cancellationToken
            )
            .ConfigureAwait(false);
        if (result.IsFailure)
        {
            WriteErrors(console, result);
            return;
        }

        console.MarkupLine(
            $"Probe [bold]{result.Value.BroadcastId:D}[/] accepted by "
                + $"[green]{result.Value.AcceptedCount}[/] of "
                + $"[bold]{result.Value.TargetCount}[/] target nodes in "
                + $"[bold]{Markup.Escape(string.Join(", ", result.Value.TargetScopes))}[/]"
        );

        if (result.Value.Nodes.Count == 0)
        {
            return;
        }

        var table = new Table()
            .Border(TableBorder.Minimal)
            .AddColumn("Node")
            .AddColumn("Outcome")
            .AddColumn("Duration")
            .AddColumn("Detail");

        foreach (var node in result.Value.Nodes)
        {
            table.AddRow(
                Markup.Escape(node.NodeIdentity),
                FormatOutcome(node.Outcome),
                node.Duration is null
                    ? "-"
                    : $"{node.Duration.Value.TotalMilliseconds.ToString("N0", CultureInfo.InvariantCulture)} ms",
                Markup.Escape(node.Detail ?? string.Empty)
            );
        }

        console.Write(table);
    }

    private static string FormatOutcome(BroadcastDeliveryOutcome outcome) =>
        outcome switch
        {
            BroadcastDeliveryOutcome.Accepted => "[green]Accepted[/]",
            BroadcastDeliveryOutcome.AlreadyProcessed => "[blue]Already processed[/]",
            BroadcastDeliveryOutcome.Expired => "[yellow]Expired[/]",
            BroadcastDeliveryOutcome.Unsupported => "[yellow]Unsupported[/]",
            BroadcastDeliveryOutcome.Rejected => "[red]Rejected[/]",
            BroadcastDeliveryOutcome.Failed => "[red]Failed[/]",
            BroadcastDeliveryOutcome.Unreachable => "[red]Unreachable[/]",
            BroadcastDeliveryOutcome.TimedOut => "[red]Timed out[/]",
            _ => Markup.Escape(outcome.ToString()),
        };
}