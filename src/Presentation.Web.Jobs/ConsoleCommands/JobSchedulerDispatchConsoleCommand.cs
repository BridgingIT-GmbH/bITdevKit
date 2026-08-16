// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Application.Jobs;
using Spectre.Console;

/// <summary>
/// Represents job scheduler dispatch console command.
/// </summary>
public class JobSchedulerDispatchConsoleCommand : JobSchedulerConsoleCommandBase
{
    /// <summary>
    /// Gets or sets the job name.
    /// </summary>
    [ConsoleCommandArgument(0, Description = "Job name", Required = true)]
    public string JobName { get; set; }

    /// <summary>
    /// Gets or sets the data.
    /// </summary>
    [ConsoleCommandOption("data", Alias = "d", Description = "Optional dispatch payload")]
    public string Data { get; set; }

    /// <summary>
    /// Gets or sets the wait.
    /// </summary>
    [ConsoleCommandOption("wait", Alias = "w", Description = "Wait for inline execution completion")]
    public bool Wait { get; set; }

    /// <summary>
    /// Gets or sets the timeout.
    /// </summary>
    [ConsoleCommandOption("timeout", Alias = "t", Description = "Wait timeout in seconds", Default = 60)]
    public int Timeout { get; set; }

    /// <summary>
    /// Initializes a new instance of the <c>JobSchedulerDispatchConsoleCommand</c> class.
    /// </summary>
    public JobSchedulerDispatchConsoleCommand()
        : base("dispatch", "Dispatch a job through its manual trigger", "trigger", "run") { }

    /// <inheritdoc/>
    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var scheduler = this.GetRequired<IJobSchedulerService>(console, services);
        if (scheduler is null)
        {
            return;
        }

        if (this.Wait)
        {
            var waitResult = await scheduler.DispatchAndWaitAsync(this.JobName, this.Data, timeout: TimeSpan.FromSeconds(Math.Max(1, this.Timeout))).ConfigureAwait(false);
            if (waitResult.IsFailure)
            {
                WriteErrors(console, waitResult);
                return;
            }

            console.MarkupLine($"Job '[bold]{Markup.Escape(this.JobName)}[/]' completed with status '[bold]{Markup.Escape(waitResult.Value.Status.ToString())}[/]'");
            return;
        }

        var result = await scheduler.DispatchAsync(this.JobName, this.Data).ConfigureAwait(false);
        if (result.IsFailure)
        {
            WriteErrors(console, result);
            return;
        }

        console.MarkupLine($"Job '[bold]{Markup.Escape(this.JobName)}[/]' dispatched as occurrence '[bold]{result.Value.OccurrenceId:D}[/]'");
    }
}
