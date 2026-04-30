// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Provides a summary view of message broker runtime state.
/// </summary>
public class BrokerMessageBrokerSummary
{
    /// <summary>
    /// Gets or sets the total number of tracked messages.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Gets or sets the number of pending messages.
    /// </summary>
    public int Pending { get; set; }

    /// <summary>
    /// Gets or sets the number of processing messages.
    /// </summary>
    public int Processing { get; set; }

    /// <summary>
    /// Gets or sets the number of succeeded messages.
    /// </summary>
    public int Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the number of failed messages.
    /// </summary>
    public int Failed { get; set; }

    /// <summary>
    /// Gets or sets the number of dead-lettered messages.
    /// </summary>
    public int DeadLettered { get; set; }

    /// <summary>
    /// Gets or sets the number of expired messages.
    /// </summary>
    public int Expired { get; set; }

    /// <summary>
    /// Gets or sets the paused message types.
    /// </summary>
    public IReadOnlyCollection<string> PausedTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the broker capabilities.
    /// </summary>
    public BrokerMessageBrokerCapabilities Capabilities { get; set; } = new();
}
