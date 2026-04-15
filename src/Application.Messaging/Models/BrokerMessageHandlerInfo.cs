// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Represents the operational view of a persisted broker handler entry.
/// </summary>
public class BrokerMessageHandlerInfo
{
    /// <summary>
    /// Gets or sets the stable subscription key.
    /// </summary>
    public string SubscriptionKey { get; set; }

    /// <summary>
    /// Gets or sets the handler type identifier.
    /// </summary>
    public string HandlerType { get; set; }

    /// <summary>
    /// Gets or sets the current handler status.
    /// </summary>
    public BrokerMessageHandlerStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the number of attempts used by this handler entry.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Gets or sets the latest handler failure summary.
    /// </summary>
    public string LastError { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the handler entry became terminal.
    /// </summary>
    public DateTimeOffset? ProcessedDate { get; set; }
}