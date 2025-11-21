// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Microsoft.Extensions.Hosting;
using Spectre.Console;
using System.Collections.Generic;
using System.Threading.Tasks;

public abstract class LogLevelGroupConsoleCommandBase(string name, string description, params string[] aliases) : ConsoleCommandBase(name, description, aliases), IGroupedConsoleCommand
{
    public string GroupName => "loglevel";

    public IReadOnlyCollection<string> GroupAliases => ["log"];

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