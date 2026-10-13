// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using System.Globalization;
using BridgingIT.DevKit.Common;
using Spectre.Console;

/// <summary>Provides shared presentation behavior for grouped profiling commands.</summary>
/// <example><code>public sealed class StatusCommand() : ProfilingConsoleCommandBase("status", "Show status");</code></example>
public abstract class ProfilingConsoleCommandBase(
    string name,
    string description,
    params string[] aliases
) : ConsoleCommandBase(name, description, aliases), IGroupedConsoleCommand
{
    /// <inheritdoc />
    public string GroupName => "profiling runtime";

    /// <inheritdoc />
    public IReadOnlyCollection<string> GroupAliases => ["prof runtime"];

    /// <summary>Resolves a command dependency and writes a safe unavailable message when absent.</summary>
    /// <typeparam name="T">The required service contract.</typeparam>
    /// <param name="console">The command output.</param>
    /// <param name="services">The command service provider.</param>
    /// <returns>The resolved dependency, or <see langword="null"/> when unavailable.</returns>
    /// <example><code>var control = GetRequired&lt;IRuntimeProfilingControlService&gt;(console, services);</code></example>
    protected static T GetRequired<T>(IAnsiConsole console, IServiceProvider services)
        where T : class
    {
        var service = services?.GetService(typeof(T)) as T;
        if (service is null)
        {
            console.MarkupLine(
                $"[yellow]Profiling unavailable:[/] {Markup.Escape(typeof(T).Name)} is not registered"
            );
        }

        return service;
    }

    /// <summary>Writes typed profiling errors without exposing implementation details.</summary>
    /// <typeparam name="T">The failed result value type.</typeparam>
    /// <param name="console">The command output.</param>
    /// <param name="result">The failed operation result.</param>
    /// <example><code>WriteErrors(console, result);</code></example>
    protected static void WriteErrors<T>(IAnsiConsole console, IResult<T> result)
    {
        var messages = result
            .Errors.SafeNull()
            .Select(error => error.Message)
            .Concat(result.Messages.SafeNull())
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .ToArray();

        if (messages.Length == 0)
        {
            console.MarkupLine("[red]Profiling operation failed[/]");
            return;
        }

        foreach (var message in messages)
        {
            console.MarkupLine($"[red]{Markup.Escape(message)}[/]");
        }
    }

    /// <summary>Writes typed non-value Profiling errors without implementation details.</summary>
    /// <param name="console">The command output.</param>
    /// <param name="result">The failed operation result.</param>
    /// <example><code>WriteErrors(console, result);</code></example>
    protected static void WriteErrors(IAnsiConsole console, IResult result)
    {
        var messages = result
            .Errors.SafeNull()
            .Select(error => error.Message)
            .Concat(result.Messages.SafeNull())
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .ToArray();

        if (messages.Length == 0)
        {
            console.MarkupLine("[red]Profiling operation failed[/]");
            return;
        }

        foreach (var message in messages)
        {
            console.MarkupLine($"[red]{Markup.Escape(message)}[/]");
        }
    }

    /// <summary>Writes an affected session followed by immediate per-node delivery outcomes.</summary>
    /// <param name="console">The command output.</param>
    /// <param name="result">The shared control-service result.</param>
    /// <example><code>WriteControlResult(console, result.Value);</code></example>
    protected static void WriteControlResult(
        IAnsiConsole console,
        RuntimeProfilingControlResult result
    )
    {
        if (result.Session is not null)
        {
            console.MarkupLine(
                $"Session [bold]{Markup.Escape(result.Session.Identity.Key)}[/] is "
                    + $"[cyan]{Markup.Escape(result.Session.State.ToString())}[/]"
                    + (result.Created ? " (created)" : string.Empty)
            );
        }

        if (result.NodeOutcomes.Count == 0)
        {
            return;
        }

        var table = new Table()
            .Border(TableBorder.Minimal)
            .AddColumn("Node")
            .AddColumn("Immediate outcome")
            .AddColumn("Duration")
            .AddColumn("Detail");

        foreach (var outcome in result.NodeOutcomes)
        {
            table.AddRow(
                Markup.Escape(outcome.NodeKey),
                FormatOutcome(outcome.Outcome),
                outcome.Duration is null
                    ? "-"
                    : $"{outcome.Duration.Value.TotalMilliseconds.ToString("N0", CultureInfo.InvariantCulture)} ms",
                Markup.Escape(outcome.Detail ?? string.Empty)
            );
        }

        console.Write(table);
        console.MarkupLine(
            "[grey]Immediate outcomes describe command delivery; Accepted does not mean local execution completed.[/]"
        );
    }

    private static string FormatOutcome(RuntimeProfilingDeliveryOutcome outcome) =>
        outcome switch
        {
            RuntimeProfilingDeliveryOutcome.Accepted => "[green]Accepted[/]",
            RuntimeProfilingDeliveryOutcome.AlreadyProcessed => "[blue]Already processed[/]",
            RuntimeProfilingDeliveryOutcome.Expired => "[yellow]Expired[/]",
            RuntimeProfilingDeliveryOutcome.Unsupported => "[yellow]Unsupported[/]",
            RuntimeProfilingDeliveryOutcome.Rejected => "[red]Rejected[/]",
            RuntimeProfilingDeliveryOutcome.Failed => "[red]Failed[/]",
            RuntimeProfilingDeliveryOutcome.Unreachable => "[red]Unreachable[/]",
            RuntimeProfilingDeliveryOutcome.TimedOut => "[red]Timed out[/]",
            _ => Markup.Escape(outcome.ToString()),
        };
}
