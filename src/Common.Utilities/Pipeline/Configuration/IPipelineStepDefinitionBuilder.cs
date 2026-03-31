// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides fluent configuration for one step definition inside a pipeline builder.
/// </summary>
public interface IPipelineStepDefinitionBuilder
{
    /// <summary>
    /// Applies a structural condition that decides whether the step is included in the built definition.
    /// </summary>
    /// <param name="condition">The structural condition to evaluate during definition building.</param>
    /// <returns>The same builder instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// builder.AddStep(() => { }, configure: step => step.When(new EnvironmentCondition("Development")));
    /// </code>
    /// </example>
    IPipelineStepDefinitionBuilder When(IPipelineDefinitionCondition condition);

    /// <summary>
    /// Attaches arbitrary metadata to the step definition.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    /// <returns>The same builder instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// builder.AddStep(() => { }, configure: step => step.WithMetadata("category", "validation"));
    /// </code>
    /// </example>
    IPipelineStepDefinitionBuilder WithMetadata(string key, object value);
}
