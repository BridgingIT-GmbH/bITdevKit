// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

/// <summary>
/// Executes registered console commands from terminal and web frontends.
/// </summary>
/// <example>
/// <code>
/// await executor.ExecuteAsync("health", console, services, ConsoleCommandExecutionSource.Terminal);
/// </code>
/// </example>
public sealed class ConsoleCommandExecutor
{
    /// <summary>
    /// Executes a command line against registered <see cref="IConsoleCommand" /> instances.
    /// </summary>
    public async Task<ConsoleCommandExecutionResult> ExecuteAsync(
        string commandLine,
        IAnsiConsole console,
        IServiceProvider services,
        ConsoleCommandExecutionSource source,
        CancellationToken cancellationToken = default)
    {
        ConsoleCommandHistory.Initialize(Assembly.GetEntryAssembly()?.GetName().Name);

        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return ConsoleCommandExecutionResult.Success();
        }

        ConsoleCommandHistory.Append(commandLine);
        var tokens = SplitArgs(commandLine);
        return await this.ExecuteAsync(tokens, console, services, source, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes pre-tokenized command arguments against registered <see cref="IConsoleCommand" /> instances.
    /// </summary>
    /// <param name="tokens">The command tokens to execute.</param>
    /// <param name="console">The console used for command output.</param>
    /// <param name="services">The service provider that contains registered commands.</param>
    /// <param name="source">The command execution source.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The command execution result.</returns>
    public async Task<ConsoleCommandExecutionResult> ExecuteAsync(
        string[] tokens,
        IAnsiConsole console,
        IServiceProvider services,
        ConsoleCommandExecutionSource source,
        CancellationToken cancellationToken = default)
    {
        if (tokens.Length == 0)
        {
            return ConsoleCommandExecutionResult.Success();
        }

        var primary = tokens[0];
        using var scope = services.CreateScope();
        var all = scope.ServiceProvider.GetServices<IConsoleCommand>().ToList();
        var consumed = 1;
        IConsoleCommand cmd = null;
        var groupedCandidates = all.OfType<IGroupedConsoleCommand>()
            .Where(g => string.Equals(g.GroupName, primary, StringComparison.OrdinalIgnoreCase) ||
                g.GroupAliases?.Any(a => string.Equals(a, primary, StringComparison.OrdinalIgnoreCase)) == true)
            .ToList();

        if (groupedCandidates.Count != 0)
        {
            if (tokens.Length == 1)
            {
                var table = new Table().Border(TableBorder.Minimal).Title($"[bold cyan]{primary} (group) subcommands[/]");
                table.AddColumn("Sub");
                table.AddColumn("Description");
                foreach (var sc in groupedCandidates.OrderBy(c => c.Name))
                {
                    table.AddRow(sc.Name, sc.Description);
                }

                console.Write(table);
                return ConsoleCommandExecutionResult.Success();
            }

            var sub = tokens[1];
            consumed = 2;
            cmd = groupedCandidates.FirstOrDefault(c => c.Matches(sub));
            if (cmd is null)
            {
                var error = $"Unknown subcommand '{sub}' for group '{primary}'.";
                console.MarkupLine($"[yellow]{Markup.Escape(error)}[/]");
                return ConsoleCommandExecutionResult.Failure(error);
            }
        }
        else
        {
            cmd = all.FirstOrDefault(c => c is not IGroupedConsoleCommand && c.Matches(primary));
        }

        if (cmd is null)
        {
            const string error = "Unknown command. Type help for list.";
            console.MarkupLine("[yellow]Unknown command[/]. Type [bold]help[/] for list.");
            return ConsoleCommandExecutionResult.Failure(error);
        }

        if (source == ConsoleCommandExecutionSource.Web && !cmd.IsWebConsoleEnabled)
        {
            const string error = "Command is not available in the web console.";
            console.MarkupLine("[yellow]Command is not available in the web console.[/]");
            return ConsoleCommandExecutionResult.Failure(error);
        }

        console.MarkupLine("[cyan]=== " + (cmd is IGroupedConsoleCommand gc ? gc.GroupName + " " + cmd.Name : cmd.Name) + " ===[/][grey] " + cmd.Description + "[/]");

        var bindTokens = tokens.Skip(consumed).ToArray();
        var (ok, errors) = ConsoleCommandBinder.TryBind(cmd, bindTokens);
        if (!ok)
        {
            foreach (var error in errors)
            {
                console.MarkupLine("[red]" + error + "[/]");
            }

            ConsoleCommandBinder.WriteHelp(console, cmd, detailed: true);
            return ConsoleCommandExecutionResult.Failure(errors.FirstOrDefault() ?? "Command arguments are invalid.");
        }

        try
        {
            cmd.OnAfterBind(console, tokens);
        }
        catch (Exception ex)
        {
            console.MarkupLine("[red]Post-bind validation failed:[/] " + ex.Message);
            return ConsoleCommandExecutionResult.Failure("Post-bind validation failed: " + ex.Message);
        }

        try
        {
            await cmd.ExecuteAsync(console, scope.ServiceProvider, cancellationToken);
            return ConsoleCommandExecutionResult.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            console.MarkupLine("[yellow]Command cancelled.[/]");
            return ConsoleCommandExecutionResult.Failure("Command cancelled.");
        }
        catch (Exception ex)
        {
            console.MarkupLine("[red]Command failed:[/] " + ex.Message);
            return ConsoleCommandExecutionResult.Failure("Command failed: " + ex.Message);
        }
    }

    /// <summary>
    /// Splits a raw command line into tokens honoring quoted segments.
    /// </summary>
    public static string[] SplitArgs(string commandLine)
    {
        var list = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        foreach (var ch in commandLine)
        {
            switch (ch)
            {
                case '"':
                    inQuotes = !inQuotes;
                    break;
                case ' ' when !inQuotes:
                    if (current.Length > 0)
                    {
                        list.Add(current.ToString());
                        current.Clear();
                    }
                    break;
                default:
                    current.Append(ch);
                    break;
            }
        }

        if (current.Length > 0)
        {
            list.Add(current.ToString());
        }

        return list.ToArray();
    }
}

/// <summary>
/// Represents the result of executing a console command.
/// </summary>
/// <param name="Succeeded">A value indicating whether execution completed successfully.</param>
/// <param name="Error">The error message when execution failed.</param>
public sealed record ConsoleCommandExecutionResult(bool Succeeded, string Error)
{
    /// <summary>
    /// Creates a successful execution result.
    /// </summary>
    /// <returns>The successful execution result.</returns>
    public static ConsoleCommandExecutionResult Success()
        => new(true, null);

    /// <summary>
    /// Creates a failed execution result.
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <returns>The failed execution result.</returns>
    public static ConsoleCommandExecutionResult Failure(string error)
        => new(false, error);
}
