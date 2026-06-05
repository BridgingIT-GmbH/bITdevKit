// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Common;
using Spectre.Console;

public abstract class OrchestrationRuntimeControlConsoleCommandBase(string name, string description, params string[] aliases) : OrchestrationConsoleCommandBase(name, description, aliases)
{
    [ConsoleCommandArgument(0, Description = "Orchestration instance id", Required = true)]
    public Guid InstanceId { get; set; }

    [ConsoleCommandOption("reason", Alias = "r", Description = "Reason recorded with the operation")]
    public string Reason { get; set; }

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

    protected abstract Task<Result> ExecuteAsync(IOrchestrationService runtime);

    protected abstract string GetSuccessMessage();
}

public class OrchestrationPauseConsoleCommand : OrchestrationRuntimeControlConsoleCommandBase
{
    public OrchestrationPauseConsoleCommand()
        : base("pause", "Pause an orchestration instance") { }

    protected override Task<Result> ExecuteAsync(IOrchestrationService runtime)
        => runtime.PauseAsync(this.InstanceId, this.Reason);

    protected override string GetSuccessMessage()
        => $"Orchestration '[bold]{this.InstanceId:D}[/]' paused";
}

public class OrchestrationResumeConsoleCommand : OrchestrationRuntimeControlConsoleCommandBase
{
    public OrchestrationResumeConsoleCommand()
        : base("resume", "Resume a paused orchestration instance", "continue") { }

    protected override Task<Result> ExecuteAsync(IOrchestrationService runtime)
        => runtime.ResumeAsync(this.InstanceId);

    protected override string GetSuccessMessage()
        => $"Orchestration '[bold]{this.InstanceId:D}[/]' resumed";
}

public class OrchestrationCancelConsoleCommand : OrchestrationRuntimeControlConsoleCommandBase
{
    public OrchestrationCancelConsoleCommand()
        : base("cancel", "Cancel an orchestration instance") { }

    protected override Task<Result> ExecuteAsync(IOrchestrationService runtime)
        => runtime.CancelAsync(this.InstanceId, this.Reason);

    protected override string GetSuccessMessage()
        => $"Orchestration '[bold]{this.InstanceId:D}[/]' cancelled";
}

public class OrchestrationTerminateConsoleCommand : OrchestrationRuntimeControlConsoleCommandBase
{
    public OrchestrationTerminateConsoleCommand()
        : base("terminate", "Terminate an orchestration instance", "stop") { }

    protected override Task<Result> ExecuteAsync(IOrchestrationService runtime)
        => runtime.TerminateAsync(this.InstanceId, this.Reason);

    protected override string GetSuccessMessage()
        => $"Orchestration '[bold]{this.InstanceId:D}[/]' terminated";
}

public abstract class OrchestrationAdministrationControlConsoleCommandBase(string name, string description, params string[] aliases) : OrchestrationConsoleCommandBase(name, description, aliases)
{
    [ConsoleCommandArgument(0, Description = "Orchestration instance id", Required = true)]
    public Guid InstanceId { get; set; }

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

    protected abstract Task<Result<string>> ExecuteAsync(IOrchestrationAdministrationService administration);
}

public class OrchestrationArchiveConsoleCommand : OrchestrationAdministrationControlConsoleCommandBase
{
    public OrchestrationArchiveConsoleCommand()
        : base("archive", "Archive a terminal orchestration instance") { }

    protected override Task<Result<string>> ExecuteAsync(IOrchestrationAdministrationService administration)
        => administration.ArchiveAsync(this.InstanceId);
}

public class OrchestrationReleaseLeaseConsoleCommand : OrchestrationAdministrationControlConsoleCommandBase
{
    public OrchestrationReleaseLeaseConsoleCommand()
        : base("release-lease", "Release an active orchestration lease", "unlock") { }

    protected override Task<Result<string>> ExecuteAsync(IOrchestrationAdministrationService administration)
        => administration.ReleaseLeaseAsync(this.InstanceId);
}

public class OrchestrationRequeueTimersConsoleCommand : OrchestrationAdministrationControlConsoleCommandBase
{
    public OrchestrationRequeueTimersConsoleCommand()
        : base("requeue-timers", "Requeue persisted orchestration timers") { }

    protected override Task<Result<string>> ExecuteAsync(IOrchestrationAdministrationService administration)
        => administration.RequeueTimersAsync(this.InstanceId);
}
