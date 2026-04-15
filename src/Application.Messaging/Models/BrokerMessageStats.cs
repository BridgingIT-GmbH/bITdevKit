// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Represents aggregate statistics for persisted broker messages.
/// </summary>
public class BrokerMessageStats
{
    /// <summary>
    /// Gets or sets the total number of matching messages.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Gets or sets the number of pending messages.
    /// </summary>
    public int Pending { get; set; }

    /// <summary>
    /// Gets or sets the number of currently processing messages.
    /// </summary>
    public int Processing { get; set; }

    /// <summary>
    /// Gets or sets the number of succeeded messages.
    /// </summary>
    public int Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the number of failed-but-retryable messages.
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
    /// Gets or sets the number of archived messages.
    /// </summary>
    public int Archived { get; set; }

    /// <summary>
    /// Gets or sets the number of currently leased messages.
    /// </summary>
    public int Leased { get; set; }
}