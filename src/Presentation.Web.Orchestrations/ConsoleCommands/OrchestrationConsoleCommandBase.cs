// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Common;
using Spectre.Console;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Represents orchestration console command base.
/// </summary>
/// <param name="name">The name of the value.</param>
/// <param name="description">The description used by the operation.</param>
/// <param name="aliases">The aliases used by the operation.</param>
public abstract class OrchestrationConsoleCommandBase(string name, string description, params string[] aliases) : ConsoleCommandBase(name, description, aliases), IGroupedConsoleCommand
{
    /// <summary>
    /// Gets the group name.
    /// </summary>
    public string GroupName => "orchestrations";

    /// <summary>
    /// Gets the group aliases.
    /// </summary>
    public IReadOnlyCollection<string> GroupAliases => ["orch"];

    /// <summary>
    /// Executes the try parse date operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <param name="result">The result used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Executes the split csv operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    protected static IReadOnlyList<string> SplitCsv(string value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Executes the write errors operation.
    /// </summary>
    /// <param name="console">The console used by the operation.</param>
    /// <param name="result">The result used by the operation.</param>
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

    /// <summary>
    /// Executes the write errors operation.
    /// </summary>
    /// <typeparam name="T">The  type.</typeparam>
    /// <param name="console">The console used by the operation.</param>
    /// <param name="result">The result used by the operation.</param>
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

    /// <summary>
    /// Executes the write paged errors operation.
    /// </summary>
    /// <typeparam name="T">The  type.</typeparam>
    /// <param name="console">The console used by the operation.</param>
    /// <param name="result">The result used by the operation.</param>
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

    /// <summary>
    /// Gets required.
    /// </summary>
    /// <typeparam name="T">The  type.</typeparam>
    /// <param name="console">The console used by the operation.</param>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The result of the operation.</returns>
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
