// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Outbox;

/// <summary>
/// Defines how persisted domain events are dispatched from the outbox.
/// </summary>
public enum OutboxDomainEventProcessMode
{
    /// <summary>
    /// Domain events are processed by the background polling service.
    /// </summary>
    Interval = 0,

    /// <summary>
    /// Domain events are queued for immediate processing after they are stored.
    /// </summary>
    Immediate = 1
}
