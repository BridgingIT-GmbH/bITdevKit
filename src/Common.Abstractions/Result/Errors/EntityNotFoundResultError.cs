// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public class EntityNotFoundResultError : ResultErrorBase
{
    public EntityNotFoundResultError()
        : base("Entity not found") { }

    public EntityNotFoundResultError(string entityType, string entityId)
        : base($"{entityType} with id {entityId} not found")
    {
        this.EntityType = entityType;
        this.EntityId = entityId;
    }

    public string EntityType { get; }

    public string EntityId { get; }
}