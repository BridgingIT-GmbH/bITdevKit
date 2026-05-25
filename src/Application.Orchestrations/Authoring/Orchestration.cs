// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

using BridgingIT.DevKit.Common;

/// <summary>
/// Provides the base class for code-first orchestration definitions.
/// </summary>
/// <typeparam name="TData">The orchestration data type.</typeparam>
/// <example>
/// <code>
/// public class SampleOrchestration : Orchestration&lt;SampleData&gt;
/// {
///     protected override void Define(IOrchestrationBuilder&lt;SampleData&gt; builder)
///     {
///         builder
///             .State("Start", state =&gt; state
///                 .Activity((context, cancellationToken) =&gt;
///                 {
///                     context.Data.Value++;
///                     return Task.FromResult(OrchestrationOutcome.Continue());
///                 })
///                 .TransitionTo("Done"))
///             .State("Done", state =&gt; state.Complete());
///     }
/// }
/// </code>
/// </example>
public abstract class Orchestration<TData> : IOrchestration<TData>
    where TData : class, IOrchestrationData
{
    private OrchestrationDefinition<TData> definition;

    /// <summary>
    /// Gets the orchestration definition name.
    /// </summary>
    public virtual string Name => this.GetType().PrettyName(false);

    /// <summary>
    /// Defines the orchestration states and transitions.
    /// </summary>
    /// <param name="builder">The orchestration builder.</param>
    protected abstract void Define(IOrchestrationBuilder<TData> builder);

    /// <summary>
    /// Gets the built orchestration definition, creating it on first access.
    /// </summary>
    /// <returns>The orchestration definition.</returns>
    public OrchestrationDefinition<TData> GetDefinition()
    {
        if (this.definition is not null)
        {
            return this.definition;
        }

        var builder = new OrchestrationDefinitionBuilder<TData>(this.Name);
        this.Define(builder);

        this.definition = builder.Build();

        return this.definition;
    }
}
