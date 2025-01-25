// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Identity;
/// <summary>
/// Provides consistent cache key generation for entity permissions.
/// </summary>
public static class EntityPermissionCacheKeys
{
    public static string Prefix = "perm";

    /// <summary>
    /// Generates a cache key for a specific user and entity combination.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="entityType">The type name of the entity.</param>
    /// <param name="entityId">The ID of the entity.</param>
    /// <returns>A cache key string.</returns>
    public static string ForUserEntity(string userId, string entityType, object entityId)
        => $"{Prefix}:user:{userId}:{entityType}:{entityId}";

    /// <summary>
    /// Generates a cache key for a user's permissions on an entity type.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="entityType">The type name of the entity.</param>
    /// <returns>A cache key string.</returns>
    public static string ForUserEntityType(string userId, string entityType)
        => $"{Prefix}:user:{userId}:{entityType}";

    /// <summary>
    /// Generates a cache key pattern for invalidating all permissions of a specific entity type.
    /// </summary>
    /// <param name="entityType">The type name of the entity.</param>
    /// <returns>A cache key pattern string.</returns>
    public static string PatternForEntityType(string entityType)
        => $"{Prefix}:*:{entityType}";
}