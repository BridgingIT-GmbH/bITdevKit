// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Common;
using Spectre.Console;

internal abstract class BroadcastingConsoleCommandBase(
    string name,
    string description,
    params string[] aliases
) : ConsoleCommandBase(name, description, aliases), IGroupedConsoleCommand
{
    public string GroupName => "broadcasting";

    public IReadOnlyCollection<string> GroupAliases => ["broadcast"];

    protected static T GetRequired<T>(IAnsiConsole console, IServiceProvider services)
        where T : class
    {
        var service = services?.GetService(typeof(T)) as T;
        if (service is null)
        {
            console.MarkupLine(
                $"[red]Error:[/] {Markup.Escape(typeof(T).Name)} is not registered or unavailable"
            );
        }

        return service;
    }

    protected static void WriteErrors<T>(IAnsiConsole console, Result<T> result)
    {
        var messages = result
            .Errors.SafeNull()
            .Select(error => error.Message)
            .Concat(result.Messages.SafeNull())
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .ToArray();

        if (messages.Length == 0)
        {
            console.MarkupLine("[red]Operation failed[/]");
            return;
        }

        foreach (var message in messages)
        {
            console.MarkupLine($"[red]{Markup.Escape(message)}[/]");
        }
    }
}