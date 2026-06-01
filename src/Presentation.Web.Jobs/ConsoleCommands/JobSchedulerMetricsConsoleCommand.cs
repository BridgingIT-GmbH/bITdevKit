// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Application.Jobs;
using Spectre.Console;

public class JobSchedulerMetricsConsoleCommand : JobSchedulerConsoleCommandBase
{
    [ConsoleCommandOption("job", Alias = "j", Description = "Filter by job name")]
    public string JobName { get; set; }

    [ConsoleCommandOption("trigger", Alias = "r", Description = "Filter by trigger name")]
    public string TriggerName { get; set; }

    public JobSchedulerMetricsConsoleCommand()
        : base("metrics", "Show aggregate job scheduler metrics", "stats") { }

    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
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
