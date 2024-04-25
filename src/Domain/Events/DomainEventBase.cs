// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using BridgingIT.DevKit.Common;
using System;

public abstract class DomainEventBase : IDomainEvent, IEquatable<DomainEventBase> // TODO: move to Domain.Mediator
{
    private int? hashCode;

    protected DomainEventBase()
    {
        this.EventId = GuidGenerator.CreateSequential();
        this.Timestamp = DateTime.UtcNow;
    }

    public Guid EventId { get; }

    public DateTimeOffset Timestamp { get; }

    public bool Equals(DomainEventBase other)
    {
        return other is not null && this.EventId.Equals(other.EventId);
    }

    public override int GetHashCode()
    {
        return this.hashCode ?? (this.hashCode = this.EventId.GetHashCode() ^ 31).Value;
    }
}