// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public class EntityDuplicateError(string message = null) : ResultErrorBase(message ?? "Duplicate")
{
    public EntityDuplicateError() : this(null)
    {
    }

    public EntityDuplicateError(string entityType, string entityId)
        : this($"{entityType} with id {entityId} duplicate")
    {
        this.EntityType = entityType;
        this.EntityId = entityId;
    }

    public string EntityType { get; }

    public string EntityId { get; }
}