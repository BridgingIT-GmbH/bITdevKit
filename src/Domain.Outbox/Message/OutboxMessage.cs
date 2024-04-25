// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Outbox;

using System;
using BridgingIT.DevKit.Domain.Model;

public class OutboxMessage : Entity<Guid> // TODO: rename to OutboxEventMessage
{
    public Guid AggregateId { get; set; }

    public string AggregateType { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public string Aggregate { get; set; } = string.Empty;

    public string AggregateEvent { get; set; } = string.Empty;

    public DateTime TimeStamp { get; set; }

    public bool IsProcessed { get; set; }

    public int RetryAttempt { get; set; }

    public Guid MessageId { get; set; }
}