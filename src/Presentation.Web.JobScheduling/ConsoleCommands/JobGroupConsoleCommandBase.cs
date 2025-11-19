// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Application.JobScheduling;
using Spectre.Console;
using System;
using System.Collections.Generic;

public abstract class JobGroupConsoleCommandBase(string name, string description, params string[] aliases) : ConsoleCommandBase(name, description, aliases), IGroupedConsoleCommand
{
    public string GroupName => "job";

    public IReadOnlyCollection<string> GroupAliases => ["j"];

    protected const string DefaultJobGroup = "DEFAULT";

    protected string NormalizeJobGroup(string jobGroup)
        => string.IsNullOrWhiteSpace(jobGroup) ? DefaultJobGroup : jobGroup;

    protected IJobService GetJobService(IServiceProvider services)
    {
        try
        {
            return services?.GetService(typeof(IJobService)) as IJobService;
        }
        catch
        {
            return null;
        }
    }

    protected async Task ExecuteWithJobServiceAsync(
        IAnsiConsole console,
        IServiceProvider services,
        Func<IJobService, Task> action)
    {
        var jobService = this.GetJobService(services);
        if (jobService == null)
        {
            console.MarkupLine("[red]Error:[/] Job scheduling service is not registered or unavailable");
            console.MarkupLine("[yellow]Ensure IJobService is registered in dependency injection[/]");
            return;
        }

        try
        {
            await action(jobService);
        }
        catch (OperationCanceledException)
        {
            console.MarkupLine("[yellow]Operation cancelled[/]");
        }
        catch (TimeoutException ex)
        {
            console.MarkupLine($"[yellow]Operation timed out:[/] {ex.Message}");
        }
        catch (Exception ex)
        {
            console.MarkupLine($"[red]Error:[/] {ex.Message}");
        }
    }

    protected async Task ExecuteWithJobServiceAsync<T>(
        IAnsiConsole console,
        IServiceProvider services,
        Func<IJobService, Task<T>> action,
        Action<T> onSuccess)
    {
        var jobService = this.GetJobService(services);
        if (jobService == null)
        {
            console.MarkupLine("[red]Error:[/] Job scheduling service is not registered or unavailable");
            console.MarkupLine("[yellow]Ensure IJobService is registered in dependency injection (services.AddJobScheduling)[/]");
            return;
        }

        try
        {
            var result = await action(jobService);
            onSuccess?.Invoke(result);
        }
        catch (OperationCanceledException)
        {
            console.MarkupLine("[yellow]Operation cancelled[/]");
        }
        catch (TimeoutException ex)
        {
            console.MarkupLine($"[yellow] Operation timed out:[/] {ex.Message}");
        }
        catch (Exception ex)
        {
            console.MarkupLine($"[red]Error:[/] {ex.Message}");
        }
    }
}
