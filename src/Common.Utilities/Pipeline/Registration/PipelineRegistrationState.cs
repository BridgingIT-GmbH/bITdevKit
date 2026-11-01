// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Stores additive pipeline registrations across repeated <c>AddPipelines()</c> calls.
/// </summary>
public class PipelineRegistrationState : IAuditBoundaryRegistrationSource
{
    private readonly List<IPipelineDefinition> definitions = [];
    private readonly List<Type> definitionSourceTypes = [];

    /// <summary>Returns bounded inline configuration and unknown packaged-source declarations without constructing definitions.</summary>
    /// <example><code>var inventory = state.Registrations;</code></example>
    public IReadOnlyList<AuditBoundaryRegistration> Registrations => this.definitions.Take(100).Select(definition =>
        new AuditBoundaryRegistration("Pipelines", "Execution", definition.BehaviorTypes.Contains(typeof(PipelineAuditingBehavior)) ? "selected" : "omitted", definition.Name.Length <= 512 ? definition.Name : "Oversized"))
        .Concat(this.definitionSourceTypes.Take(100).Select(type => new AuditBoundaryRegistration("Pipelines", "DefinitionSource", "unknown", type.Name))).Take(100).ToArray();

    /// <summary>
    /// Gets the inline-built pipeline definitions accumulated during registration.
    /// </summary>
    public IReadOnlyList<IPipelineDefinition> Definitions => this.definitions;

    /// <summary>
    /// Gets the packaged pipeline definition source types accumulated during registration.
    /// </summary>
    public IReadOnlyList<Type> DefinitionSourceTypes => this.definitionSourceTypes;

    /// <summary>
    /// Adds a built pipeline definition to the additive registration state.
    /// </summary>
    /// <param name="definition">The definition to add.</param>
    public void AddDefinition(IPipelineDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        this.definitions.Add(definition);
    }

    /// <summary>
    /// Adds a packaged pipeline definition source type to the additive registration state.
    /// </summary>
    /// <param name="definitionSourceType">The packaged definition source type to add.</param>
    public void AddDefinitionSourceType(Type definitionSourceType)
    {
        ArgumentNullException.ThrowIfNull(definitionSourceType);
        this.definitionSourceTypes.Add(definitionSourceType);
    }
}
