// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Identity;

using System.Diagnostics;

/// <summary>
/// Represents detailed information about a permission granted to a specific entity.
/// </summary>
[DebuggerDisplay("{EntityType} [{Permission}] from {Source} [{SourceId}]")]
public class EntityPermissionInfo
{
    /// <summary>
    /// Gets or sets the permission value.
    /// </summary>
    public string Permission { get; set; }

    /// <summary>
    /// Gets or sets the source of the permission (e.g., "Direct", "Role:Admins", "Parent", "Default:ReadOnlyProvider").
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the ID of the source (e.g., role id, parent entity id).
    /// </summary>
    //public object SourceId { get; set; }

    /// <summary>
    /// Gets or sets the entity type this permission applies to.
    /// </summary>
    public string EntityType { get; set; }

    ///// <summary>
    ///// Gets or sets the entity ID this permission applies to (null for type-wide permissions).
    ///// </summary>
    //public object EntityId { get; set; }
}
