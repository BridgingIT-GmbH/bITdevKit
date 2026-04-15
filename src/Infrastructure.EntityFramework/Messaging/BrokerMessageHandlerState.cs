// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Messaging;

using BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Stores the durable execution state of a single subscribed handler for a broker message.
/// </summary>
public class BrokerMessageHandlerState
{
    /// <summary>
    /// Gets or sets the stable identifier for the subscription snapshot entry.
    /// </summary>
    public string SubscriptionKey { get; set; }

    /// <summary>
    /// Gets or sets the fully qualified handler type name used for diagnostics and targeting retries.
    /// </summary>
    public string HandlerType { get; set; }

    /// <summary>
    /// Gets or sets the current lifecycle status for this handler entry.
    /// </summary>
    public BrokerMessageHandlerStatus Status { get; set; } = BrokerMessageHandlerStatus.Pending;

    /// <summary>
    /// Gets or sets the number of processing attempts performed for this handler entry.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Gets or sets the latest failure message for this handler entry.
    /// </summary>
    public string LastError { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this handler entry reached a terminal state.
    /// </summary>
    public DateTimeOffset? ProcessedDate { get; set; }
}