// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Identity;
using System.Collections.Generic;
using BridgingIT.DevKit.Domain.Model;

/// <summary>
/// Default implementation of IDefaultEntityPermissionProvider that uses configured permissions.
/// </summary>
public class DefaultPermissionProvider<TEntity>(EntityPermissionOptions options)
    : IDefaultEntityPermissionProvider<TEntity>
    where TEntity : class, IEntity
{
    private readonly HashSet<string> permissions =
        options.DefaultEntityPermissions.TryGetValue(typeof(TEntity), out var entityPermissions)
            ? entityPermissions
            : [];

    /// <summary>
    /// Gets the set of default permissions for the entity type.
    /// </summary>
    /// <returns>A set of default permissions.</returns>
    public HashSet<string> GetDefaultPermissions() => this.permissions;
}
