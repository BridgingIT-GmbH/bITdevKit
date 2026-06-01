// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Common;
using Spectre.Console;

public abstract class JobSchedulerOccurrenceControlConsoleCommandBase(string name, string description, params string[] aliases) : JobSchedulerConsoleCommandBase(name, description, aliases)
{
    [ConsoleCommandArgument(0, Description = "Occurrence id", Required = true)]
    public Guid OccurrenceId { get; set; }

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

public class JobSchedulerStopConsoleCommand : JobSchedulerOccurrenceControlConsoleCommandBase
{
    public JobSchedulerStopConsoleCommand()
        : base("stop", "Interrupt a running occurrence", "interrupt") { }

    protected override Task<Result> ExecuteAsync(IJobSchedulerService scheduler)
        => scheduler.InterruptOccurrenceAsync(this.OccurrenceId, this.Reason);

    protected override string GetSuccessMessage()
        => $"Occurrence '[bold]{this.OccurrenceId:D}[/]' interruption requested";
}

public class JobSchedulerCancelConsoleCommand : JobSchedulerOccurrenceControlConsoleCommandBase
{
    public JobSchedulerCancelConsoleCommand()
        : base("cancel", "Cancel an occurrence") { }

    protected override Task<Result> ExecuteAsync(IJobSchedulerService scheduler)
        => scheduler.CancelOccurrenceAsync(this.OccurrenceId, this.Reason);

    protected override string GetSuccessMessage()
        => $"Occurrence '[bold]{this.OccurrenceId:D}[/]' cancellation requested";
}

public class JobSchedulerRetryConsoleCommand : JobSchedulerOccurrenceControlConsoleCommandBase
{
    public JobSchedulerRetryConsoleCommand()
        : base("retry", "Retry a failed occurrence") { }

    protected override Task<Result> ExecuteAsync(IJobSchedulerService scheduler)
        => scheduler.RetryOccurrenceAsync(this.OccurrenceId, this.Reason);

    protected override string GetSuccessMessage()
        => $"Occurrence '[bold]{this.OccurrenceId:D}[/]' scheduled for retry";
}

public class JobSchedulerArchiveConsoleCommand : JobSchedulerOccurrenceControlConsoleCommandBase
{
    public JobSchedulerArchiveConsoleCommand()
        : base("archive", "Archive an occurrence") { }

    protected override Task<Result> ExecuteAsync(IJobSchedulerService scheduler)
        => scheduler.ArchiveOccurrenceAsync(this.OccurrenceId, this.Reason);

    protected override string GetSuccessMessage()
        => $"Occurrence '[bold]{this.OccurrenceId:D}[/]' archived";
}

public class JobSchedulerReleaseLeaseConsoleCommand : JobSchedulerOccurrenceControlConsoleCommandBase
{
    public JobSchedulerReleaseLeaseConsoleCommand()
        : base("release-lease", "Release an occurrence lease", "unlock") { }

    protected override Task<Result> ExecuteAsync(IJobSchedulerService scheduler)
        => scheduler.ReleaseOccurrenceLeaseAsync(this.OccurrenceId, this.Reason);

    protected override string GetSuccessMessage()
        => $"Lease for occurrence '[bold]{this.OccurrenceId:D}[/]' released";
}
