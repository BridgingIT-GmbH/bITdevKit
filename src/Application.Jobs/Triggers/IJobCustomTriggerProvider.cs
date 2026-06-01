// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Provides custom trigger evaluation behavior without leaking provider-specific types into the scheduler core.
/// </summary>
public interface IJobCustomTriggerProvider
{
    /// <summary>
    /// Evaluates the custom trigger and returns occurrences to materialize.
    /// </summary>
    /// <param name="context">The trigger evaluation context.</param>
    /// <returns>The updated runtime state and occurrences to materialize.</returns>
    Result<JobTriggerEvaluationResult> Materialize(JobTriggerEvaluationContext context);
}