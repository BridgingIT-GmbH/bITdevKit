// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using BridgingIT.DevKit.Common;
using System;
using System.Diagnostics;

[DebuggerDisplay("EventId={EventId}")]
public abstract class DomainEventBase
    : IDomainEvent, IEquatable<DomainEventBase>
{
    private int? hashCode;

    public virtual Guid EventId { get; protected set; } = GuidGenerator.CreateSequential();

    public virtual DateTimeOffset Timestamp { get; protected set; } = DateTime.UtcNow;

    public bool Equals(DomainEventBase other)
    {
        return other is not null && this.EventId.Equals(other.EventId);
    }

    public override int GetHashCode()
    {
        return this.hashCode ?? (this.hashCode = this.EventId.GetHashCode() ^ 31).Value;
    }
}