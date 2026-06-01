// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Common;
using Spectre.Console;

public abstract class JobSchedulerJobControlConsoleCommandBase(string name, string description, params string[] aliases) : JobSchedulerConsoleCommandBase(name, description, aliases)
{
    [ConsoleCommandArgument(0, Description = "Job name", Required = true)]
    public string JobName { get; set; }

    [ConsoleCommandOption("reason", Alias = "r", Description = "Reason recorded with the operation")]
    public string Reason { get; set; }

    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        var scheduler = this.GetRequired<IJobSchedulerService>(console, services);
        if (scheduler is null)
        {
            return;
        }

        var result = await this.ExecuteAsync(scheduler).ConfigureAwait(false);
        if (result.IsFailure)
        {
            WriteErrors(console, result);
            return;
        }

        console.MarkupLine(this.GetSuccessMessage());
    }

    protected abstract Task<Result> ExecuteAsync(IJobSchedulerService scheduler);

    protected abstract string GetSuccessMessage();
}

public class JobSchedulerPauseConsoleCommand : JobSchedulerJobControlConsoleCommandBase
{
    public JobSchedulerPauseConsoleCommand()
        : base("pause", "Pause a registered job") { }

    protected override Task<Result> ExecuteAsync(IJobSchedulerService scheduler)
        => scheduler.PauseJobAsync(this.JobName, this.Reason);

    protected override string GetSuccessMessage()
        => $"Job '[bold]{Markup.Escape(this.JobName)}[/]' paused";
}

public class JobSchedulerResumeConsoleCommand : JobSchedulerJobControlConsoleCommandBase
{
    public JobSchedulerResumeConsoleCommand()
        : base("resume", "Resume a paused job", "continue") { }

    protected override Task<Result> ExecuteAsync(IJobSchedulerService scheduler)
        => scheduler.ResumeJobAsync(this.JobName, this.Reason);

    protected override string GetSuccessMessage()
        => $"Job '[bold]{Markup.Escape(this.JobName)}[/]' resumed";
}

public class JobSchedulerEnableConsoleCommand : JobSchedulerJobControlConsoleCommandBase
{
    public JobSchedulerEnableConsoleCommand()
        : base("enable", "Enable a registered job") { }

    protected override Task<Result> ExecuteAsync(IJobSchedulerService scheduler)
        => scheduler.EnableJobAsync(this.JobName, this.Reason);

    protected override string GetSuccessMessage()
        => $"Job '[bold]{Markup.Escape(this.JobName)}[/]' enabled";
}

public class JobSchedulerDisableConsoleCommand : JobSchedulerJobControlConsoleCommandBase
{
    public JobSchedulerDisableConsoleCommand()
        : base("disable", "Disable a registered job") { }

    protected override Task<Result> ExecuteAsync(IJobSchedulerService scheduler)
        => scheduler.DisableJobAsync(this.JobName, this.Reason);

    protected override string GetSuccessMessage()
        => $"Job '[bold]{Markup.Escape(this.JobName)}[/]' disabled";
}
