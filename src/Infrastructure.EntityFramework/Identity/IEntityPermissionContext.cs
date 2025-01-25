// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// Defines the contract for a database context that manages entity permissions.
/// This interface should be implemented by DbContext classes that need to handle permission storage.
/// </summary>
public interface IEntityPermissionContext
{
    /// <summary>
    /// Gets or sets the DbSet of EntityPermissions that represents the permission entries in the database.
    /// </summary>
    /// <value>
    /// A DbSet of EntityPermission objects that can be used to query and save permissions.
    /// </value>
    DbSet<EntityPermission> EntityPermissions { get; set; }
}