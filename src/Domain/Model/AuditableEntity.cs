// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

/// <summary>
///     Represents an abstract base class for auditable entities, inheriting from the Entity class
///     and implementing the IAuditable interface.
/// </summary>
/// <typeparam name="TId">The type of the unique identifier for the entity.</typeparam>
[DebuggerDisplay("Type={GetType().Name}, Id={Id}")]
public abstract class AuditableEntity<TId> : Entity<TId>, IAuditable
{
    /// <summary>
    ///     Gets or sets the state of auditing for the entity.
    ///     This property is used to track audit information such as creation, modification, and deletion details.
    /// </summary>
    public AuditState AuditState { get; set; } = new();
}

/// <summary>
///     Represents an abstract base class for entities that support auditing.
/// </summary>
/// <typeparam name="TId">The type of the entity's identifier.</typeparam>
//[Obsolete("Just use AuditableEntity<TId> from now on")]
[DebuggerDisplay("Type={GetType().Name}, Id={Id}")]
public abstract class AuditableEntity<TId, TIdType> : AuditableEntity<TId>
    where TId : EntityId<TIdType>
{
    /// <summary>
    ///     Gets or sets the unique identifier for the entity.
    /// </summary>
    /// <remarks>
    ///     This property is used to uniquely identify instances of the entity.
    /// </remarks>
    public new EntityId<TIdType> Id { get; set; }
}