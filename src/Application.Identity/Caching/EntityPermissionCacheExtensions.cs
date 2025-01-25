// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Identity;

using BridgingIT.DevKit.Common;

/// <summary>
/// Provides extension methods for cache invalidation specific to permissions.
/// </summary>
public static class EntityPermissionCacheExtensions
{
    /// <summary>
    /// Invalidates all permission cache entries for a specific entity type.
    /// </summary>
    /// <param name="cacheProvider">The cache provider.</param>
    /// <param name="entityType">The entity type to invalidate permissions for.</param>
    public static void InvalidateEntityTypePermissions(this ICacheProvider cacheProvider, string entityType)
    {
        var pattern = EntityPermissionCacheKeys.PatternForEntityType(entityType);
        cacheProvider.RemoveStartsWith(pattern);
    }

    /// <summary>
    /// Invalidates all permission cache entries for a specific user.
    /// </summary>
    /// <param name="cacheProvider">The cache provider.</param>
    /// <param name="userId">The user ID to invalidate permissions for.</param>
    public static void InvalidateUserPermissions(this ICacheProvider cacheProvider, string userId)
    {
        var pattern = $"{EntityPermissionCacheKeys.Prefix}:user:{userId}";
        cacheProvider.RemoveStartsWith(pattern);
    }
}