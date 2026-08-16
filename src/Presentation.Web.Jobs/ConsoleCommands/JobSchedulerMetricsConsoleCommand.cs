// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Application.Jobs;
using Spectre.Console;

/// <summary>
/// Represents job scheduler metrics console command.
/// </summary>
public class JobSchedulerMetricsConsoleCommand : JobSchedulerConsoleCommandBase
{
    /// <summary>
    /// Gets or sets the job name.
    /// </summary>
    [ConsoleCommandOption("job", Alias = "j", Description = "Filter by job name")]
    public string JobName { get; set; }

    /// <summary>
    /// Gets or sets the trigger name.
    /// </summary>
    [ConsoleCommandOption("trigger", Alias = "r", Description = "Filter by trigger name")]
    public string TriggerName { get; set; }

    /// <summary>
    /// Initializes a new instance of the <c>JobSchedulerMetricsConsoleCommand</c> class.
    /// </summary>
    public JobSchedulerMetricsConsoleCommand()
        : base("metrics", "Show aggregate job scheduler metrics", "stats") { }

    /// <inheritdoc/>
    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var query = this.GetRequired<IJobSchedulerQueryService>(console, services);
        if (query is null)
        {
            return;
        }

        var result = await query.GetMetricsAsync(new JobSchedulerMetricsRequest
        {
            JobName = this.JobName,
            TriggerName = this.TriggerName
        }).ConfigureAwait(false);

        if (result.IsFailure)
        {
            WriteErrors(console, result);
            return;
        }

        console.Write(JobSchedulerTableBuilders.BuildMetricsTable(result.Value));
    }
}
