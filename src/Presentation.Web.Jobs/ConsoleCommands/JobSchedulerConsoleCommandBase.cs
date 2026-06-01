// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Common;
using Spectre.Console;
using System.Collections.Generic;
using System.Globalization;

public abstract class JobSchedulerConsoleCommandBase(string name, string description, params string[] aliases) : ConsoleCommandBase(name, description, aliases), IGroupedConsoleCommand
{
    public string GroupName => "jobs";

    public IReadOnlyCollection<string> GroupAliases => [];

    protected static bool TryParseDate(string value, out DateTimeOffset? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return false;
        }

        result = parsed;
        return true;
    }

    protected static void WriteErrors(IAnsiConsole console, Result result)
    {
        var messages = result.Errors.SafeNull()
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

    protected static void WriteErrors<T>(IAnsiConsole console, Result<T> result)
    {
        var messages = result.Errors.SafeNull()
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

    protected static void WritePagedErrors<T>(IAnsiConsole console, ResultPaged<T> result)
    {
        var messages = result.Errors.SafeNull()
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

    protected T GetRequired<T>(IAnsiConsole console, IServiceProvider services)
        where T : class
    {
        var service = services?.GetService(typeof(T)) as T;
        if (service is null)
        {
            console.MarkupLine($"[red]Error:[/] {typeof(T).Name} is not registered or unavailable");
        }

        return service;
    }
}
