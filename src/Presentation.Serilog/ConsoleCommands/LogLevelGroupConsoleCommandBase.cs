// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Microsoft.Extensions.Hosting;
using Spectre.Console;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Base class for console commands that manipulate Serilog log levels.
/// </summary>
/// <remarks>
/// Provides grouping ("loglevel" / alias "log") and a helper method to execute logic with a <see cref="LogLevelManager"/> instance.
/// </remarks>
/// <param name="name">Primary command name.</param>
/// <param name="description">Short description displayed in help output.</param>
/// <param name="aliases">Optional aliases that also trigger the command.</param>
public abstract class LogLevelGroupConsoleCommandBase(string name, string description, params string[] aliases) : ConsoleCommandBase(name, description, aliases), IGroupedConsoleCommand
{
    /// <summary>
    /// Gets the logical group name for all log level related commands.
    /// </summary>
    public string GroupName => "loglevel";

    /// <summary>
    /// Gets the aliases that can be used for the group (e.g. in grouped help listings).
    /// </summary>
    public IReadOnlyCollection<string> GroupAliases => ["log"];

    /// <summary>
    /// Executes the specified asynchronous action with a configured <see cref="LogLevelManager"/>.
    /// </summary>
    /// <param name="console">Spectre console instance used for output formatting.</param>
    /// <param name="action">The action to run with a <see cref="LogLevelManager"/>.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// Catches and writes exceptions as console markup. If Serilog was not initialized, an error is displayed.
    /// </remarks>
    protected async Task ExecuteWithLogLevelManagerAsync(
        IAnsiConsole console,
        Func<LogLevelManager, Task> action)
    {
        try
        {
            var manager = new LogLevelManager(LogLevelSwitchProvider.GetControlSwitch());
            await action(manager);
        }
        catch (InvalidOperationException ex)
        {
            console.MarkupLine("[red]Error:[/] " + ex.Message.Replace("[]", string.Empty));
        }
        catch (Exception ex)
        {
            console.MarkupLine("[red]Error:[/] " + ex.Message.Replace("[]", string.Empty));
        }
    }
}