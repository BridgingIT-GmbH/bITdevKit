﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

using Common;
using Domain.Model;
using Messaging;

public class EntityDeletedMessage<TEntity>(TEntity entity) : MessageBase
    where TEntity : class, IEntity
{
    public TEntity Entity { get; } = entity;

    public string EntityId { get; } = entity?.Id?.ToString();

    public string EntityType { get; } = entity?.GetType().PrettyName();
}