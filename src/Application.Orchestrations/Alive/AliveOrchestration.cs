// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

/// <summary>
/// Represents the input for the built-in orchestration alive probe.
/// </summary>
/// <example>
/// <code>
/// var data = new AliveOrchestrationData { Source = "dashboard" };
/// </code>
/// </example>
public sealed class AliveOrchestrationData : IOrchestrationData
{
    /// <summary>
    /// Gets or sets the component that requested the probe.
    /// </summary>
    public string Source { get; set; } = "dashboard";

    /// <summary>
    /// Gets or sets the correlation identifier assigned to this probe.
    /// </summary>
    public string CorrelationId { get; set; } = GuidGenerator.CreateSequential().ToString("N");
}

/// <summary>
/// Provides the built-in orchestration alive probe definition.
/// </summary>
/// <example>
/// <code>
/// services.AddOrchestrations().WithOrchestration&lt;AliveOrchestration&gt;();
/// </code>
/// </example>
public sealed class AliveOrchestration : Orchestration<AliveOrchestrationData>
{
    /// <inheritdoc />
    public override string Name => "alive-orchestration";

    /// <inheritdoc />
    protected override void Define(IOrchestrationBuilder<AliveOrchestrationData> builder)
    {
        builder
            .State("Received", state => state
                .Activity(async (context, cancellationToken) =>
                {
                    context.Data.Source = string.IsNullOrWhiteSpace(context.Data.Source)
                        ? "dashboard"
                        : context.Data.Source.Trim();
                    context.Data.CorrelationId = string.IsNullOrWhiteSpace(context.Data.CorrelationId)
                        ? GuidGenerator.CreateSequential().ToString("N")
                        : context.Data.CorrelationId.Trim();

                    await Task.Delay(600, cancellationToken); // Simulate some work

                    return OrchestrationOutcome.Continue();
                }, "mark-alive")
                .TransitionTo("Completed"))
            .State("Completed", state => state.Complete("Alive orchestration completed."));
    }
}
