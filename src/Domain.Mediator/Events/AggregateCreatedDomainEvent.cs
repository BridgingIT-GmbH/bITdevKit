// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using BridgingIT.DevKit.Domain.Model;
using EnsureThat;

public class AggregateCreatedDomainEvent<TEntity> : DomainEventBase
    where TEntity : class, IEntity, IAggregateRoot
{
    public AggregateCreatedDomainEvent(TEntity entity)
    {
        EnsureArg.IsNotNull(entity, nameof(entity));

        this.Entity = entity;
    }

    public TEntity Entity { get; }
}