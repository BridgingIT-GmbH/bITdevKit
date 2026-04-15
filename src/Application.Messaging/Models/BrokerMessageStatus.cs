// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Represents the aggregate processing lifecycle of a persisted broker message.
/// </summary>
public enum BrokerMessageStatus
{
    /// <summary>
    /// The message is stored and still has retryable handler work remaining.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The message is currently leased and being processed by a worker.
    /// </summary>
    Processing = 1,

    /// <summary>
    /// All subscribed handlers completed successfully.
    /// </summary>
    Succeeded = 2,

    /// <summary>
    /// The message encountered a non-terminal processing failure and remains eligible for retry.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// One or more handlers exhausted retry attempts and the message is terminal.
    /// </summary>
    DeadLettered = 4,

    /// <summary>
    /// The message expired before all handlers completed.
    /// </summary>
    Expired = 5
}