// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Identity;

/// <summary>
/// Represents a cached set of permissions.
/// </summary>
public class EntityPermissionCacheEntry
{
    /// <summary>
    /// Gets or sets the set of granted permissions.
    /// </summary>
    public HashSet<string> Permissions { get; set; } = [];

    /// <summary>
    /// Gets or sets the timestamp when this cache entry was last updated.
    /// </summary>
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets a dictionary of permission sources, mapping permission values to their source
    /// (e.g., "Direct", "Role", "Wildcard", "Default").
    /// </summary>
    public Dictionary<string, string> PermissionSources { get; set; } = [];

    /// <summary>
    /// Creates a new instance of the PermissionCacheEntry class.
    /// </summary>
    public EntityPermissionCacheEntry()
    {
    }

    /// <summary>
    /// Creates a new instance of the PermissionCacheEntry class with the specified permissions.
    /// </summary>
    /// <param name="permissions">The initial set of permissions.</param>
    public EntityPermissionCacheEntry(IEnumerable<string> permissions)
    {
        this.Permissions = [.. permissions];
    }

    /// <summary>
    /// Adds a permission with its source to the cache entry.
    /// </summary>
    /// <param name="permission">The permission to add.</param>
    /// <param name="source">The source of the permission.</param>
    public void AddPermission(string permission, string source)
    {
        this.Permissions.Add(permission);
        this.PermissionSources[permission] = source;
        this.LastUpdated = DateTimeOffset.UtcNow;
    }
}
