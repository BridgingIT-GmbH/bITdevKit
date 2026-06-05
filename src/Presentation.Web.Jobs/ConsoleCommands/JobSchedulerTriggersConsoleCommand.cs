// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Common;
using Spectre.Console;

public class JobSchedulerTriggersConsoleCommand : JobSchedulerConsoleCommandBase
{
    [ConsoleCommandArgument(0, Description = "Job name", Required = false)]
    public string JobName { get; set; }

    [ConsoleCommandOption("name", Alias = "n", Description = "Filter by trigger name")]
    public string TriggerName { get; set; }

    [ConsoleCommandOption("recurring", Alias = "r", Description = "Show recurring triggers only")]
    public bool Recurring { get; set; }

    [ConsoleCommandOption("take", Alias = "t", Description = "Max items to show", Default = 50)]
    public int Take { get; set; }

    public JobSchedulerTriggersConsoleCommand()
        : base("triggers", "List job triggers", "trigger-list") { }

    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var query = this.GetRequired<IJobSchedulerQueryService>(console, services);
        if (query is null)
        {
            return;
        }

        ResultPaged<JobSchedulerTriggerModel> result;
        if (this.Recurring)
        {
            var recurring = await query.QueryRecurringTriggersAsync(new JobSchedulerRecurringTriggerQueryRequest
            {
                JobName = this.JobName,
                TriggerName = this.TriggerName,
                Take = Math.Max(1, this.Take),
                SortBy = "JobName",
                SortDescending = false
            }).ConfigureAwait(false);

            if (recurring.IsFailure)
            {
                WritePagedErrors(console, recurring);
                return;
            }

            result = ResultPaged<JobSchedulerTriggerModel>.Success(recurring.Value?.Cast<JobSchedulerTriggerModel>().ToList() ?? []);
        }
        else
        {
            result = await query.QueryTriggersAsync(new JobSchedulerTriggerQueryRequest
            {
                JobName = this.JobName,
                TriggerName = this.TriggerName,
                Take = Math.Max(1, this.Take),
                SortBy = "JobName",
                SortDescending = false
            }).ConfigureAwait(false);
        }

        if (result.IsFailure)
        {
            WritePagedErrors(console, result);
            return;
        }

        var triggers = result.Value?.ToList() ?? [];
        if (triggers.Count == 0)
        {
            console.MarkupLine("[yellow]No triggers found[/]");
            return;
        }

        console.Write(JobSchedulerTableBuilders.BuildTriggersTable(triggers));
    }
}
