// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Represents in process message broker configuration.
/// </summary>
public class InProcessMessageBrokerConfiguration
{
    /// <summary>
    /// Gets or sets the process delay.
    /// </summary>
    public int ProcessDelay { get; set; }

    /// <summary>
    /// Gets or sets the message expiration.
    /// </summary>
    public TimeSpan? MessageExpiration { get; set; }
}
