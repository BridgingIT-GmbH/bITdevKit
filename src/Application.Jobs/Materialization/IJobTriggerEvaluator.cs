// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Evaluates trigger definitions and creates occurrences without executing them.
/// </summary>
public interface IJobTriggerEvaluator
{
    /// <summary>
    /// Evaluates the supplied trigger.
    /// </summary>
    /// <param name="job">The owning job definition.</param>
    /// <param name="trigger">The trigger definition.</param>
    /// <param name="request">The evaluation request.</param>
    /// <returns>The updated runtime state and occurrences to persist.</returns>
    Result<JobTriggerEvaluationResult> Materialize(
        JobDefinition job,
        JobTriggerDefinition trigger,
        JobTriggerEvaluationRequest request);
}