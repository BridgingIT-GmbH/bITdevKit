// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Application.JobScheduling;
using Spectre.Console;
using System;
using System.Collections.Generic;

/// <summary>
/// Represents job group console command base.
/// </summary>
/// <param name="name">The name of the value.</param>
/// <param name="description">The description used by the operation.</param>
/// <param name="aliases">The aliases used by the operation.</param>
public abstract class JobGroupConsoleCommandBase(string name, string description, params string[] aliases) : ConsoleCommandBase(name, description, aliases), IGroupedConsoleCommand
{
    /// <summary>
    /// Gets the group name.
    /// </summary>
    public string GroupName => "job";

    /// <summary>
    /// Gets the group aliases.
    /// </summary>
    public IReadOnlyCollection<string> GroupAliases => ["j"];

    /// <summary>
    /// Defines the default job group value.
    /// </summary>
    protected const string DefaultJobGroup = "DEFAULT";

    /// <summary>
    /// Executes the normalize job group operation.
    /// </summary>
    /// <param name="jobGroup">The job group used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    protected string NormalizeJobGroup(string jobGroup)
        => string.IsNullOrWhiteSpace(jobGroup) ? DefaultJobGroup : jobGroup;

    /// <summary>
    /// Gets job service.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>
    /// Executes the execute with job service operation.
    /// </summary>
    /// <param name="console">The console used by the operation.</param>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="action">The action to invoke.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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

    /// <summary>
    /// Executes the execute with job service operation.
    /// </summary>
    /// <typeparam name="T">The  type.</typeparam>
    /// <param name="console">The console used by the operation.</param>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="action">The action to invoke.</param>
    /// <param name="onSuccess">The on success used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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
