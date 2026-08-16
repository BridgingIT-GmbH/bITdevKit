// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Represents a requested entity that could not be found.</summary>
/// <param name="message">The missing-entity description, or <see langword="null"/> to use <c>Not found</c>.</param>
public class EntityNotFoundError(string message = null) : ResultErrorBase(message ?? "Not found")
{
    /// <summary>Initializes a missing-entity error with the default message.</summary>
    public EntityNotFoundError() : this(null)
    {
    }

    /// <summary>Initializes an error whose message identifies the missing entity by type and identifier.</summary>
    /// <param name="entityType">The entity type name included in the generated message.</param>
    /// <param name="entityId">The entity identifier included in the generated message.</param>
    public EntityNotFoundError(string entityType, string entityId)
        : this($"{entityType} with id {entityId} not found")
    {
        this.EntityType = entityType;
        this.EntityId = entityId;
    }

    /// <summary>Gets the entity type name, when initialized by type and identifier.</summary>
    public string EntityType { get; }

    /// <summary>Gets the entity identifier, when initialized by type and identifier.</summary>
    public string EntityId { get; }
}
