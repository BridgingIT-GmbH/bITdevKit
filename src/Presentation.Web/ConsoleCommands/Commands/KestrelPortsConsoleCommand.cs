// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

public class KestrelPortsConsoleCommand : ConsoleCommandBase
{
    /// <summary>
    /// Lists currently bound Kestrel addresses/ports.
    /// Usage: <c>ports</c>
    /// Aliases: <c>urls</c>, <c>kestrel</c>
    /// </summary>
    public KestrelPortsConsoleCommand() : base("ports", "Show Kestrel bound addresses", "urls", "kestrel") { }

    /// <summary>Executes ports listing.</summary>
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        var server = services.GetService<IServer>();
        var feature = server?.Features.Get<IServerAddressesFeature>();
        var addresses = feature?.Addresses ?? Array.Empty<string>();
        if (addresses.Count == 0)
        {
            console.MarkupLine("[yellow]No addresses available yet.[/]");

            return Task.CompletedTask;
        }
        var table = new Table().Border(TableBorder.Minimal);
        table.AddColumn("Address");
        foreach (var a in addresses)
        {
            table.AddRow(a);
        }

        console.Write(table);

        return Task.CompletedTask;
    }
}
// === end diagnostic additions ===