// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using System.Globalization;
using BridgingIT.DevKit.Common;
using Spectre.Console;

internal sealed class BroadcastingListConsoleCommand()
    : BroadcastingConsoleCommandBase("list", "List broadcast node registrations", "nodes")
{
    public override async Task ExecuteAsync(
        IAnsiConsole console,
        IServiceProvider services,
        CancellationToken cancellationToken = default
    )
    {
        var diagnostics = GetRequired<IBroadcastingDiagnostics>(console, services);
        if (diagnostics is null)
        {
            return;
        }

        var snapshot = await diagnostics
            .GetAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!snapshot.Enabled)
        {
            console.MarkupLine("[yellow]Broadcasting is disabled[/]");
            return;
        }

        var registrations = snapshot
            .Scopes.SelectMany(scope => scope.Nodes)
            .GroupBy(node => node.NodeIdentity, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var registration = group
                    .OrderByDescending(node => node.RegisteredUtc)
                    .First();
                var scopes = group
                    .SelectMany(node => node.Scopes)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(scope => scope, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return (Registration: registration, Scopes: scopes);
            })
            .OrderByDescending(item => item.Registration.IsActive)
            .ThenBy(item => item.Registration.NodeIdentity, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (registrations.Length == 0)
        {
            console.MarkupLine("[yellow]No broadcast node registrations found[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Minimal)
            .AddColumn("State")
            .AddColumn("Node")
            .AddColumn("Scopes")
            .AddColumn("Receiver")
            .AddColumn("Failures")
            .AddColumn("Registered");

        foreach (var item in registrations)
        {
            var registration = item.Registration;
            table.AddRow(
                registration.IsActive ? "[green]Active[/]" : "[grey]Inactive[/]",
                Markup.Escape(registration.NodeIdentity),
                Markup.Escape(string.Join(", ", item.Scopes)),
                registration.AdvertisedAddress is null
                    ? "[grey]Local only[/]"
                    : Markup.Escape(registration.AdvertisedAddress.ToString()),
                registration.ConsecutiveFailureCount > 0
                    ? $"[red]{registration.ConsecutiveFailureCount}[/]"
                    : "0",
                registration.RegisteredUtc.LocalDateTime.ToString(
                    "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture
                )
            );
        }

        console.Write(table);
    }
}