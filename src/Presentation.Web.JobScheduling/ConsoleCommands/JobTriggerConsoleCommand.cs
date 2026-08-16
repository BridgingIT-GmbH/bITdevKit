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
/// Represents job trigger console command.
/// </summary>
public class JobTriggerConsoleCommand : JobGroupConsoleCommandBase
{
    /// <summary>
    /// Gets or sets the job name.
    /// </summary>
    [ConsoleCommandArgument(0, Description = "Job name", Required = true)]
    public string JobName { get; set; }

    /// <summary>
    /// Gets or sets the job group.
    /// </summary>
    [ConsoleCommandArgument(1, Description = "Job group", Required = false)]
    public string JobGroup { get; set; }

    /// <summary>
    /// Gets or sets the data.
    /// </summary>
    [ConsoleCommandOption("data", Alias = "d", Description = "Job data as key=value pairs")]
    public string[] Data { get; set; }

    /// <summary>
    /// Gets or sets the wait.
    /// </summary>
    [ConsoleCommandOption("wait", Alias = "w", Description = "Wait for job completion")]
    public bool Wait { get; set; }

    /// <summary>
    /// Gets or sets the timeout.
    /// </summary>
    [ConsoleCommandOption("timeout", Alias = "t", Description = "Timeout in milliseconds (default 600000)")]
    public int Timeout { get; set; } = 600000;

    /// <summary>
    /// Initializes a new instance of the <c>JobTriggerConsoleCommand</c> class.
    /// </summary>
    public JobTriggerConsoleCommand() : base("trigger", "Trigger job execution") { }

    /// <inheritdoc/>
    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(this.JobName))
        {
            console.MarkupLine("[red]Job name is required[/]");

            return;
        }

        await this.ExecuteWithJobServiceAsync(console, services, async jobService =>
        {
            var jobGroup = this.NormalizeJobGroup(this.JobGroup);
            var jobData = this.ParseJobData(this.Data);

            if (this.Wait)
            {
                await this.ExecuteWithWait(console, jobService, jobGroup, jobData);
            }
            else
            {
                await jobService.TriggerJobAsync(this.JobName, jobGroup, jobData);

                console.MarkupLine($"Job '[bold]{this.JobName}[/]' triggered in group '[bold]{jobGroup}[/]'");
            }
        });
    }

    private async Task ExecuteWithWait(
        IAnsiConsole console,
        IJobService jobService,
        string jobGroup,
        IDictionary<string, object> jobData)
    {
        console.MarkupLine($"[cyan]Triggering and waiting for job '[bold]{this.JobName}[/]'...[/]");

        try
        {
            var result = await jobService.TriggerJobAndWaitAsync(
                this.JobName,
                jobGroup,
                jobData,
                checkInterval: 500,
                timeout: System.TimeSpan.FromMilliseconds(this.Timeout));

            var statusIcon = result.LastRun?.Status == "Completed" ? "[green]✓[/]" : "[red]✗[/]";
            console.MarkupLine($"{statusIcon} Job completed with status: [bold]{result.LastRun?.Status}[/]");
            console.MarkupLine($"Duration: {result.LastRun?.DurationText}");

            if (!string.IsNullOrEmpty(result.LastRun?.Result))
            {
                console.MarkupLine($"Result: {result.LastRun.Result}");
            }
        }
        catch (TimeoutException)
        {
            console.MarkupLine("[yellow]Job execution timed out[/]");
        }
    }

    private IDictionary<string, object> ParseJobData(string[] data)
    {
        var result = new Dictionary<string, object>();

        if (data == null || data.Length == 0)
        {
            return result;
        }

        foreach (var item in data)
        {
            var parts = item.Split('=', 2);
            if (parts.Length == 2)
            {
                result[parts[0].Trim()] = parts[1].Trim();
            }
        }

        return result;
    }
}
