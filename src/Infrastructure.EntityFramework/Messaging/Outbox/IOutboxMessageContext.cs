// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

/// <summary>
/// Defines operations for i outbox message context.
/// </summary>
public interface IOutboxMessageContext
{
    /// <summary>
    /// Gets or sets the outbox messages.
    /// </summary>
    DbSet<OutboxMessage> OutboxMessages { get; set; }
}
