// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Outbox.Models;

using System;

public class Outbox
{
    public Guid Id { get; set; }

    public Guid AggregateId { get; set; }

    public string AggregateType { get; set; }

    public string EventType { get; set; }

    public string Aggregate { get; set; }

    public string AggregateEvent { get; set; }

    public DateTime TimeStamp { get; set; }

    public bool IsProcessed { get; set; }

    public int RetryAttempt { get; set; }
}