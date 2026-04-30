// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Describes a registered message subscription.
/// </summary>
public class BrokerMessageSubscriptionInfo
{
    /// <summary>
    /// Gets or sets the message type name.
    /// </summary>
    public string MessageType { get; set; }

    /// <summary>
    /// Gets or sets the handler type name.
    /// </summary>
    public string HandlerType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the message type is paused.
    /// </summary>
    public bool IsMessageTypePaused { get; set; }
}
