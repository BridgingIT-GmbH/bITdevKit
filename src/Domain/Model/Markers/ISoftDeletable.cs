// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

/// <summary>
///     Represents an entity that supports soft deletion functionality.
/// </summary>
public interface ISoftDeletable // or use IAuditable (see AuditableEntity)
{
    /// <summary>
    ///     Represents the soft delete state of an entity.
    ///     True indicates the entity is soft deleted, while False or Null indicates the entity is not soft deleted.
    /// </summary>
    bool? Deleted { get; }

    /// <summary>
    ///     Sets the 'Deleted' flag of an entity that implements ISoftDeletable.
    /// </summary>
    void SetDeleted(bool value = true);
}