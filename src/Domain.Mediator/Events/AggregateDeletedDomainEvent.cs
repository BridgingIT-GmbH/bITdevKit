// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

public class AggregateDeletedDomainEvent<TEntity>(TEntity entity) : DomainEventBase
    where TEntity : class, IEntity, IAggregateRoot
{
    public TEntity Entity { get; } = entity;
}