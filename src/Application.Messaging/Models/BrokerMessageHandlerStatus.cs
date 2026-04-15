// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Represents the processing lifecycle of a single subscribed handler entry inside a persisted broker message.
/// </summary>
public enum BrokerMessageHandlerStatus
{
    /// <summary>
    /// The handler has not yet completed and remains eligible for processing.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The handler is currently being executed by the owning worker.
    /// </summary>
    Processing = 1,

    /// <summary>
    /// The handler completed successfully.
    /// </summary>
    Succeeded = 2,

    /// <summary>
    /// The handler failed and may be retried.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// The handler exhausted retry attempts and is terminal.
    /// </summary>
    DeadLettered = 4,

    /// <summary>
    /// The handler was never executed because the message expired.
    /// </summary>
    Expired = 5
}