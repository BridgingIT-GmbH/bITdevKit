// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Application.Orchestrations;
using Spectre.Console;

/// <summary>
/// Represents orchestration signal console command.
/// </summary>
public class OrchestrationSignalConsoleCommand : OrchestrationConsoleCommandBase
{
    /// <summary>
    /// Gets or sets the instance id.
    /// </summary>
    [ConsoleCommandArgument(0, Description = "Orchestration instance id", Required = true)]
    public Guid InstanceId { get; set; }

    /// <summary>
    /// Gets or sets the signal name.
    /// </summary>
    [ConsoleCommandArgument(1, Description = "Signal name", Required = true)]
    public string SignalName { get; set; }

    /// <summary>
    /// Gets or sets the payload.
    /// </summary>
    [ConsoleCommandOption("payload", Alias = "p", Description = "Optional signal payload")]
    public string Payload { get; set; }

    /// <summary>
    /// Gets or sets the idempotency key.
    /// </summary>
    [ConsoleCommandOption("idempotency-key", Alias = "i", Description = "Optional idempotency key")]
    public string IdempotencyKey { get; set; }

    /// <summary>
    /// Initializes a new instance of the <c>OrchestrationSignalConsoleCommand</c> class.
    /// </summary>
    public OrchestrationSignalConsoleCommand()
        : base("signal", "Deliver a signal to an orchestration instance") { }

    /// <inheritdoc/>
    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var runtime = this.GetRequired<IOrchestrationService>(console, services);
        if (runtime is null)
        {
            return;
        }

        var result = await runtime.SignalAsync(this.InstanceId, this.SignalName, this.Payload, this.IdempotencyKey).ConfigureAwait(false);
        if (result.IsFailure)
        {
            WriteErrors(console, result);
            return;
        }

        console.MarkupLine($"Signal '[bold]{Markup.Escape(this.SignalName)}[/]' accepted for orchestration '[bold]{this.InstanceId:D}[/]'");
    }
}
