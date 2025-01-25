// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Identity;

using BridgingIT.DevKit.Domain.Model;

/// <summary>
/// Defines the contract for providing default permissions for an entity type.
/// </summary>
/// <typeparam name="TEntity">The type of entity for which default permissions are provided.</typeparam>
public interface IDefaultEntityPermissionProvider<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    /// Gets the set of default permissions for the entity type.
    /// </summary>
    /// <returns>A set of default permissions.</returns>
    HashSet<string> GetDefaultPermissions();
}