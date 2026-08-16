// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Common;
using Spectre.Console;

/// <summary>
/// Represents orchestration runtime control console command base.
/// </summary>
/// <param name="name">The name of the value.</param>
/// <param name="description">The description used by the operation.</param>
/// <param name="aliases">The aliases used by the operation.</param>
public abstract class OrchestrationRuntimeControlConsoleCommandBase(string name, string description, params string[] aliases) : OrchestrationConsoleCommandBase(name, description, aliases)
{
    /// <summary>
    /// Gets or sets the instance id.
    /// </summary>
    [ConsoleCommandArgument(0, Description = "Orchestration instance id", Required = true)]
    public Guid InstanceId { get; set; }

    /// <summary>
    /// Gets or sets the reason.
    /// </summary>
    [ConsoleCommandOption("reason", Alias = "r", Description = "Reason recorded with the operation")]
    public string Reason { get; set; }

    /// <inheritdoc/>
    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var runtime = this.GetRequired<IOrchestrationService>(console, services);
        if (runtime is null)
        {
            return;
        }

        var result = await this.ExecuteAsync(runtime).ConfigureAwait(false);
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
    /// <param name="runtime">The runtime used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected abstract Task<Result> ExecuteAsync(IOrchestrationService runtime);

    /// <summary>
    /// Gets success message.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    protected abstract string GetSuccessMessage();
}

/// <summary>
/// Represents orchestration pause console command.
/// </summary>
public class OrchestrationPauseConsoleCommand : OrchestrationRuntimeControlConsoleCommandBase
{
    /// <summary>
    /// Initializes a new instance of the <c>OrchestrationPauseConsoleCommand</c> class.
    /// </summary>
    public OrchestrationPauseConsoleCommand()
        : base("pause", "Pause an orchestration instance") { }

    /// <inheritdoc/>
    protected override Task<Result> ExecuteAsync(IOrchestrationService runtime)
        => runtime.PauseAsync(this.InstanceId, this.Reason);

    /// <inheritdoc/>
    protected override string GetSuccessMessage()
        => $"Orchestration '[bold]{this.InstanceId:D}[/]' paused";
}

/// <summary>
/// Represents orchestration resume console command.
/// </summary>
public class OrchestrationResumeConsoleCommand : OrchestrationRuntimeControlConsoleCommandBase
{
    /// <summary>
    /// Initializes a new instance of the <c>OrchestrationResumeConsoleCommand</c> class.
    /// </summary>
    public OrchestrationResumeConsoleCommand()
        : base("resume", "Resume a paused orchestration instance", "continue") { }

    /// <inheritdoc/>
    protected override Task<Result> ExecuteAsync(IOrchestrationService runtime)
        => runtime.ResumeAsync(this.InstanceId);

    /// <inheritdoc/>
    protected override string GetSuccessMessage()
        => $"Orchestration '[bold]{this.InstanceId:D}[/]' resumed";
}

/// <summary>
/// Represents orchestration cancel console command.
/// </summary>
public class OrchestrationCancelConsoleCommand : OrchestrationRuntimeControlConsoleCommandBase
{
    /// <summary>
    /// Initializes a new instance of the <c>OrchestrationCancelConsoleCommand</c> class.
    /// </summary>
    public OrchestrationCancelConsoleCommand()
        : base("cancel", "Cancel an orchestration instance") { }

    /// <inheritdoc/>
    protected override Task<Result> ExecuteAsync(IOrchestrationService runtime)
        => runtime.CancelAsync(this.InstanceId, this.Reason);

    /// <inheritdoc/>
    protected override string GetSuccessMessage()
        => $"Orchestration '[bold]{this.InstanceId:D}[/]' cancelled";
}

/// <summary>
/// Represents orchestration terminate console command.
/// </summary>
public class OrchestrationTerminateConsoleCommand : OrchestrationRuntimeControlConsoleCommandBase
{
    /// <summary>
    /// Initializes a new instance of the <c>OrchestrationTerminateConsoleCommand</c> class.
    /// </summary>
    public OrchestrationTerminateConsoleCommand()
        : base("terminate", "Terminate an orchestration instance", "stop") { }

    /// <inheritdoc/>
    protected override Task<Result> ExecuteAsync(IOrchestrationService runtime)
        => runtime.TerminateAsync(this.InstanceId, this.Reason);

    /// <inheritdoc/>
    protected override string GetSuccessMessage()
        => $"Orchestration '[bold]{this.InstanceId:D}[/]' terminated";
}

/// <summary>
/// Represents orchestration administration control console command base.
/// </summary>
/// <param name="name">The name of the value.</param>
/// <param name="description">The description used by the operation.</param>
/// <param name="aliases">The aliases used by the operation.</param>
public abstract class OrchestrationAdministrationControlConsoleCommandBase(string name, string description, params string[] aliases) : OrchestrationConsoleCommandBase(name, description, aliases)
{
    /// <summary>
    /// Gets or sets the instance id.
    /// </summary>
    [ConsoleCommandArgument(0, Description = "Orchestration instance id", Required = true)]
    public Guid InstanceId { get; set; }

    /// <inheritdoc/>
    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var administration = this.GetRequired<IOrchestrationAdministrationService>(console, services);
        if (administration is null)
        {
            return;
        }

        var result = await this.ExecuteAsync(administration).ConfigureAwait(false);
        if (result.IsFailure)
        {
            WriteErrors(console, result);
            return;
        }

        console.MarkupLine(Markup.Escape(result.Value));
    }

    /// <summary>
    /// Executes the execute operation.
    /// </summary>
    /// <param name="administration">The administration used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected abstract Task<Result<string>> ExecuteAsync(IOrchestrationAdministrationService administration);
}

/// <summary>
/// Represents orchestration archive console command.
/// </summary>
public class OrchestrationArchiveConsoleCommand : OrchestrationAdministrationControlConsoleCommandBase
{
    /// <summary>
    /// Initializes a new instance of the <c>OrchestrationArchiveConsoleCommand</c> class.
    /// </summary>
    public OrchestrationArchiveConsoleCommand()
        : base("archive", "Archive a terminal orchestration instance") { }

    /// <inheritdoc/>
    protected override Task<Result<string>> ExecuteAsync(IOrchestrationAdministrationService administration)
        => administration.ArchiveAsync(this.InstanceId);
}

/// <summary>
/// Represents orchestration release lease console command.
/// </summary>
public class OrchestrationReleaseLeaseConsoleCommand : OrchestrationAdministrationControlConsoleCommandBase
{
    /// <summary>
    /// Initializes a new instance of the <c>OrchestrationReleaseLeaseConsoleCommand</c> class.
    /// </summary>
    public OrchestrationReleaseLeaseConsoleCommand()
        : base("release-lease", "Release an active orchestration lease", "unlock") { }

    /// <inheritdoc/>
    protected override Task<Result<string>> ExecuteAsync(IOrchestrationAdministrationService administration)
        => administration.ReleaseLeaseAsync(this.InstanceId);
}

/// <summary>
/// Represents orchestration requeue timers console command.
/// </summary>
public class OrchestrationRequeueTimersConsoleCommand : OrchestrationAdministrationControlConsoleCommandBase
{
    /// <summary>
    /// Initializes a new instance of the <c>OrchestrationRequeueTimersConsoleCommand</c> class.
    /// </summary>
    public OrchestrationRequeueTimersConsoleCommand()
        : base("requeue-timers", "Requeue persisted orchestration timers") { }

    /// <inheritdoc/>
    protected override Task<Result<string>> ExecuteAsync(IOrchestrationAdministrationService administration)
        => administration.RequeueTimersAsync(this.InstanceId);
}
