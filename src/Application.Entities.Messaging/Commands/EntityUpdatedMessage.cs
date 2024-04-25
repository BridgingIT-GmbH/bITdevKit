// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;

public class EntityUpdatedMessage<TEntity> : MessageBase
    where TEntity : class, IEntity
{
    public EntityUpdatedMessage(TEntity entity)
    {
        this.Entity = entity;
        this.EntityId = entity?.Id?.ToString();
        this.EntityType = entity?.GetType().PrettyName();
    }

    public TEntity Entity { get; }

    public string EntityId { get; }

    public string EntityType { get; }
}