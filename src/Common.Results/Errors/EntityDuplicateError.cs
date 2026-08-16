// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Represents an attempt to create or register an entity that already exists.</summary>
/// <param name="message">The duplicate-entity description, or <see langword="null"/> to use <c>Duplicate</c>.</param>
public class EntityDuplicateError(string message = null) : ResultErrorBase(message ?? "Duplicate")
{
    /// <summary>Initializes a duplicate-entity error with the default message.</summary>
    public EntityDuplicateError() : this(null)
    {
    }

    /// <summary>Initializes an error whose message identifies a duplicate entity by type and identifier.</summary>
    /// <param name="entityType">The entity type name included in the generated message.</param>
    /// <param name="entityId">The entity identifier included in the generated message.</param>
    public EntityDuplicateError(string entityType, string entityId)
        : this($"{entityType} with id {entityId} duplicate")
    {
        this.EntityType = entityType;
        this.EntityId = entityId;
    }

    /// <summary>Gets the entity type name, when initialized by type and identifier.</summary>
    public string EntityType { get; }

    /// <summary>Gets the entity identifier, when initialized by type and identifier.</summary>
    public string EntityId { get; }
}
