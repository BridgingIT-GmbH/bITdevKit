// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Represents a trigger evaluation request.
/// </summary>
public sealed record JobTriggerEvaluationRequest
{
    /// <summary>
    /// Gets the prior runtime state.
    /// </summary>
    public JobTriggerRuntimeState RuntimeState { get; init; } = JobTriggerRuntimeState.Empty;

    /// <summary>
    /// Gets the explicit activation instant used for delayed trigger anchoring.
    /// </summary>
    public DateTimeOffset? ActivationUtc { get; init; }

    /// <summary>
    /// Gets the scheduler startup instant used for startup-delay anchoring.
    /// </summary>
    public DateTimeOffset? SchedulerStartedUtc { get; init; }

    /// <summary>
    /// Gets a value indicating whether a manual dispatch was explicitly requested.
    /// </summary>
    public bool ManualDispatchRequested { get; init; }

    /// <summary>
    /// Gets an optional dispatch instant override.
    /// </summary>
    public DateTimeOffset? DispatchRequestedUtc { get; init; }

    /// <summary>
    /// Gets an optional dispatch identity used to derive a deterministic occurrence key.
    /// </summary>
    public string DispatchIdentity { get; init; }

    /// <summary>
    /// Gets optional override data for explicit dispatch.
    /// </summary>
    public object OverrideData { get; init; }

    /// <summary>
    /// Gets optional override properties for explicit dispatch.
    /// </summary>
    public PropertyBag OverrideProperties { get; init; }

    /// <summary>
    /// Gets the safety limit for catch-up materialization.
    /// </summary>
    public int MaxCatchUpOccurrences { get; init; } = 100;
}