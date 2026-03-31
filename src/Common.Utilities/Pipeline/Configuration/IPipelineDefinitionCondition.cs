// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a structural condition that decides whether a step is included in a built pipeline definition.
/// </summary>
public interface IPipelineDefinitionCondition
{
    /// <summary>
    /// Determines whether the condition is satisfied for the current definition-building context.
    /// </summary>
    /// <param name="context">The definition-building context.</param>
    /// <returns><see langword="true"/> when the step should be included; otherwise <see langword="false"/>.</returns>
    bool IsSatisfied(PipelineDefinitionContext context);
}
