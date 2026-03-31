// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Stores fluent configuration for one step definition while the pipeline builder is composing a definition.
/// </summary>
public class PipelineStepDefinitionBuilderState : IPipelineStepDefinitionBuilder
{
    private readonly Dictionary<string, object> metadata = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the configured structural condition.
    /// </summary>
    public IPipelineDefinitionCondition Condition { get; private set; }

    /// <summary>
    /// Gets the configured step metadata.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata => this.metadata;

    /// <inheritdoc />
    public IPipelineStepDefinitionBuilder When(IPipelineDefinitionCondition condition)
    {
        this.Condition = condition;
        return this;
    }

    /// <inheritdoc />
    public IPipelineStepDefinitionBuilder WithMetadata(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Metadata key cannot be empty.", nameof(key));
        }

        this.metadata[key] = value;
        return this;
    }
}
