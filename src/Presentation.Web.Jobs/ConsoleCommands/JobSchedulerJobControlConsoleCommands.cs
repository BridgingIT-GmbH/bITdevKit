// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Common;
using Spectre.Console;

/// <summary>
/// Represents job scheduler job control console command base.
/// </summary>
/// <param name="name">The name of the value.</param>
/// <param name="description">The description used by the operation.</param>
/// <param name="aliases">The aliases used by the operation.</param>
public abstract class JobSchedulerJobControlConsoleCommandBase(string name, string description, params string[] aliases) : JobSchedulerConsoleCommandBase(name, description, aliases)
{
    /// <summary>
    /// Gets or sets the job name.
    /// </summary>
    [ConsoleCommandArgument(0, Description = "Job name", Required = true)]
    public string JobName { get; set; }

    /// <summary>
    /// Gets or sets the reason.
    /// </summary>
    [ConsoleCommandOption("reason", Alias = "r", Description = "Reason recorded with the operation")]
    public string Reason { get; set; }

    /// <inheritdoc/>
    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
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

    /// <summary>
    /// Executes the execute operation.
    /// </summary>
    /// <param name="scheduler">The scheduler used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected abstract Task<Result> ExecuteAsync(IJobSchedulerService scheduler);

    /// <summary>
    /// Gets success message.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    protected abstract string GetSuccessMessage();
}

/// <summary>
/// Represents job scheduler pause console command.
/// </summary>
public class JobSchedulerPauseConsoleCommand : JobSchedulerJobControlConsoleCommandBase
{
    /// <summary>
    /// Initializes a new instance of the <c>JobSchedulerPauseConsoleCommand</c> class.
    /// </summary>
    public JobSchedulerPauseConsoleCommand()
        : base("pause", "Pause a registered job") { }

    /// <inheritdoc/>
    protected override Task<Result> ExecuteAsync(IJobSchedulerService scheduler)
        => scheduler.PauseJobAsync(this.JobName, this.Reason);

    /// <inheritdoc/>
    protected override string GetSuccessMessage()
        => $"Job '[bold]{Markup.Escape(this.JobName)}[/]' paused";
}

/// <summary>
/// Represents job scheduler resume console command.
/// </summary>
public class JobSchedulerResumeConsoleCommand : JobSchedulerJobControlConsoleCommandBase
{
    /// <summary>
    /// Initializes a new instance of the <c>JobSchedulerResumeConsoleCommand</c> class.
    /// </summary>
    public JobSchedulerResumeConsoleCommand()
        : base("resume", "Resume a paused job", "continue") { }

    /// <inheritdoc/>
    protected override Task<Result> ExecuteAsync(IJobSchedulerService scheduler)
        => scheduler.ResumeJobAsync(this.JobName, this.Reason);

    /// <inheritdoc/>
    protected override string GetSuccessMessage()
        => $"Job '[bold]{Markup.Escape(this.JobName)}[/]' resumed";
}

/// <summary>
/// Represents job scheduler enable console command.
/// </summary>
public class JobSchedulerEnableConsoleCommand : JobSchedulerJobControlConsoleCommandBase
{
    /// <summary>
    /// Initializes a new instance of the <c>JobSchedulerEnableConsoleCommand</c> class.
    /// </summary>
    public JobSchedulerEnableConsoleCommand()
        : base("enable", "Enable a registered job") { }

    /// <inheritdoc/>
    protected override Task<Result> ExecuteAsync(IJobSchedulerService scheduler)
        => scheduler.EnableJobAsync(this.JobName, this.Reason);

    /// <inheritdoc/>
    protected override string GetSuccessMessage()
        => $"Job '[bold]{Markup.Escape(this.JobName)}[/]' enabled";
}

/// <summary>
/// Represents job scheduler disable console command.
/// </summary>
public class JobSchedulerDisableConsoleCommand : JobSchedulerJobControlConsoleCommandBase
{
    /// <summary>
    /// Initializes a new instance of the <c>JobSchedulerDisableConsoleCommand</c> class.
    /// </summary>
    public JobSchedulerDisableConsoleCommand()
        : base("disable", "Disable a registered job") { }

    /// <inheritdoc/>
    protected override Task<Result> ExecuteAsync(IJobSchedulerService scheduler)
        => scheduler.DisableJobAsync(this.JobName, this.Reason);

    /// <inheritdoc/>
    protected override string GetSuccessMessage()
        => $"Job '[bold]{Markup.Escape(this.JobName)}[/]' disabled";
}
