// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using System.Collections.Generic;

/// <summary>
/// Represents requester info console command.
/// </summary>
public class RequesterInfoConsoleCommand : ConsoleCommandBase, IGroupedConsoleCommand
{
    /// <summary>
    /// Initializes a new instance of the <c>RequesterInfoConsoleCommand</c> class.
    /// </summary>
    public RequesterInfoConsoleCommand() : base("list", "List registrations") { }

    /// <summary>
    /// Gets the group name.
    /// </summary>
    public string GroupName => "requester";

    /// <summary>
    /// Gets the group aliases.
    /// </summary>
    public IReadOnlyCollection<string> GroupAliases => ["req"];

    /// <inheritdoc/>
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var requester = services.GetRequiredService<IRequester>();
        if (requester != null)
        {
            var infos = requester.GetRegistrationInformation();
        }
        else
        {
            console.MarkupLine("[red]No requester registered.[/]");
        }

        var cache = services.GetRequiredService<IHandlerCache>();
        if (cache != null)
        {
            var table = new Table().Border(TableBorder.Minimal);
            table.AddColumn("Request/Notification");
            table.AddColumn("Handler");
            table.AddColumn("Behaviors");

            foreach (var kvp in cache.OrderBy(x => x.Key.FullName))
            {
                var requestType = kvp.Key.IsGenericType
                    ? kvp.Key.GetGenericArguments()[0].PrettyName()
                    : kvp.Key.PrettyName();

                var attributes = kvp.Value
                    .GetCustomAttributes(inherit: false)
                    .Select(a => a.GetType().Name.Replace("Attribute", ""))
                    .Where(n => !string.IsNullOrEmpty(n))
                    .ToArray();

                var attributeNames = attributes.Length > 0
                    ? string.Join(", ", attributes)
                    : "-";

                table.AddRow(requestType, kvp.Value.PrettyName(), attributeNames);
            }

            console.Write(table);
        }

        return Task.CompletedTask;
    }
}
