// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

/// <summary>
/// Represents the current lifecycle status of an orchestration instance.
/// </summary>
public enum OrchestrationStatus
{
    /// <summary>
    /// The orchestration context has been created but execution has not started yet.
    /// </summary>
    Created,

    /// <summary>
    /// The orchestration is actively executing activities or transitions.
    /// </summary>
    Running,

    /// <summary>
    /// The orchestration is waiting for an external release condition.
    /// </summary>
    Waiting,

    /// <summary>
    /// The orchestration has been paused externally.
    /// </summary>
    Paused,

    /// <summary>
    /// The orchestration completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// The orchestration failed because execution could not continue safely.
    /// </summary>
    Failed,

    /// <summary>
    /// The orchestration ended in a cancelled state.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The orchestration ended in a terminated state.
    /// </summary>
    Terminated,
}