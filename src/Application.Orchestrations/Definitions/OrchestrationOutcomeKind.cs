// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

/// <summary>
/// Represents the outcome produced by an orchestration activity.
/// </summary>
public enum OrchestrationOutcomeKind
{
    /// <summary>
    /// Continue with the next activity or state transition evaluation.
    /// </summary>
    Continue,

    /// <summary>
    /// Retry the current activity immediately.
    /// </summary>
    Retry,

    /// <summary>
    /// Enter a waiting state.
    /// </summary>
    Wait,

    /// <summary>
    /// Complete the orchestration successfully.
    /// </summary>
    Complete,

    /// <summary>
    /// Cancel the orchestration.
    /// </summary>
    Cancel,

    /// <summary>
    /// Terminate the orchestration.
    /// </summary>
    Terminate,
}