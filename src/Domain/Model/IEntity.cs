// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

/// <summary>
///     Represents a basic entity with an identifier.
/// </summary>
public interface IEntity
{
    /// <summary>
    ///     Gets or sets the unique identifier for the entity.
    /// </summary>
    /// <value>
    ///     The unique identifier.
    /// </value>
    object Id { get; set; }
}

/// <summary>
///     Represents a generic entity in the domain model with an identifier of type object.
/// </summary>
public interface IEntity<TId> : IEntity
{
    /// <summary>
    ///     Gets or sets the unique identifier of the entity.
    /// </summary>
    /// <value>
    ///     The unique identifier.
    /// </value>
    new TId Id { get; set; }
}