// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Common;
using Spectre.Console;

/// <summary>
/// Represents job scheduler occurrence control console command base.
/// </summary>
/// <param name="name">The name of the value.</param>
/// <param name="description">The description used by the operation.</param>
/// <param name="aliases">The aliases used by the operation.</param>
public abstract class JobSchedulerOccurrenceControlConsoleCommandBase(string name, string description, params string[] aliases) : JobSchedulerConsoleCommandBase(name, description, aliases)
{
    /// <summary>
    /// Gets or sets the occurrence id.
    /// </summary>
    [ConsoleCommandArgument(0, Description = "Occurrence id", Required = true)]
    public Guid OccurrenceId { get; set; }

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
/// Represents job scheduler stop console command.
/// </summary>
public class JobSchedulerStopConsoleCommand : JobSchedulerOccurrenceControlConsoleCommandBase
{
    /// <summary>
    /// Initializes a new instance of the <c>JobSchedulerStopConsoleCommand</c> class.
    /// </summary>
    public JobSchedulerStopConsoleCommand()
        : base("stop", "Interrupt a running occurrence", "interrupt") { }

    /// <inheritdoc/>
    protected override Task<Result> ExecuteAsync(IJobSchedulerService scheduler)
        => scheduler.InterruptOccurrenceAsync(this.OccurrenceId, this.Reason);

    /// <inheritdoc/>
    protected override string GetSuccessMessage()
        => $"Occurrence '[bold]{this.OccurrenceId:D}[/]' interruption requested";
}

/// <summary>
/// Represents job scheduler cancel console command.
/// </summary>
public class JobSchedulerCancelConsoleCommand : JobSchedulerOccurrenceControlConsoleCommandBase
{
    /// <summary>
    /// Initializes a new instance of the <c>JobSchedulerCancelConsoleCommand</c> class.
    /// </summary>
    public JobSchedulerCancelConsoleCommand()
        : base("cancel", "Cancel an occurrence") { }

    /// <inheritdoc/>
    protected override Task<Result> ExecuteAsync(IJobSchedulerService scheduler)
        => scheduler.CancelOccurrenceAsync(this.OccurrenceId, this.Reason);

    /// <inheritdoc/>
    protected override string GetSuccessMessage()
        => $"Occurrence '[bold]{this.OccurrenceId:D}[/]' cancellation requested";
}

/// <summary>
/// Represents job scheduler retry console command.
/// </summary>
public class JobSchedulerRetryConsoleCommand : JobSchedulerOccurrenceControlConsoleCommandBase
{
    /// <summary>
    /// Initializes a new instance of the <c>JobSchedulerRetryConsoleCommand</c> class.
    /// </summary>
    public JobSchedulerRetryConsoleCommand()
        : base("retry", "Retry a failed occurrence") { }

    /// <inheritdoc/>
    protected override Task<Result> ExecuteAsync(IJobSchedulerService scheduler)
        => scheduler.RetryOccurrenceAsync(this.OccurrenceId, this.Reason);

    /// <inheritdoc/>
    protected override string GetSuccessMessage()
        => $"Occurrence '[bold]{this.OccurrenceId:D}[/]' scheduled for retry";
}

/// <summary>
/// Represents job scheduler archive console command.
/// </summary>
public class JobSchedulerArchiveConsoleCommand : JobSchedulerOccurrenceControlConsoleCommandBase
{
    /// <summary>
    /// Initializes a new instance of the <c>JobSchedulerArchiveConsoleCommand</c> class.
    /// </summary>
    public JobSchedulerArchiveConsoleCommand()
        : base("archive", "Archive an occurrence") { }

    /// <inheritdoc/>
    protected override Task<Result> ExecuteAsync(IJobSchedulerService scheduler)
        => scheduler.ArchiveOccurrenceAsync(this.OccurrenceId, this.Reason);

    /// <inheritdoc/>
    protected override string GetSuccessMessage()
        => $"Occurrence '[bold]{this.OccurrenceId:D}[/]' archived";
}

/// <summary>
/// Represents job scheduler release lease console command.
/// </summary>
public class JobSchedulerReleaseLeaseConsoleCommand : JobSchedulerOccurrenceControlConsoleCommandBase
{
    /// <summary>
    /// Initializes a new instance of the <c>JobSchedulerReleaseLeaseConsoleCommand</c> class.
    /// </summary>
    public JobSchedulerReleaseLeaseConsoleCommand()
        : base("release-lease", "Release an occurrence lease", "unlock") { }

    /// <inheritdoc/>
    protected override Task<Result> ExecuteAsync(IJobSchedulerService scheduler)
        => scheduler.ReleaseOccurrenceLeaseAsync(this.OccurrenceId, this.Reason);

    /// <inheritdoc/>
    protected override string GetSuccessMessage()
        => $"Lease for occurrence '[bold]{this.OccurrenceId:D}[/]' released";
}
