// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Identity;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using Microsoft.AspNetCore.Authorization;

public static class AuthorizationExtensions
{
    /// <summary>
    /// Adds a policy to the <see cref="AuthorizationOptions"/> for entity permission requirements.
    /// </summary>
    /// <param name="options">The <see cref="AuthorizationOptions"/> to add the policy to.</param>
    /// <param name="permissions">The permissions that identifies the entity permission requirement.</param>
    public static AuthorizationOptions AddEntityPermissionPolicy<TEntity>(this AuthorizationOptions options, params Permission[] permissions)
        where TEntity : class, IEntity
    {
        return options.AddEntityPermissionPolicy(typeof(TEntity), permissions);
    }

    /// <summary>
    /// Adds a policy to the <see cref="AuthorizationOptions"/> for entity permission requirements.
    /// </summary>
    /// <param name="options">The <see cref="AuthorizationOptions"/> to add the policy to.</param>
    /// <param name="entityType">The entity for the permssions</param>
    /// <param name="permissions">The permissions that identifies the entity permission requirement.</param>
    public static AuthorizationOptions AddEntityPermissionPolicy(this AuthorizationOptions options, Type entityType, params Permission[] permissions)
    {
        foreach (var permission in permissions.SafeNull())
        {
            options.AddPolicy($"{nameof(EntityPermissionRequirement)}_{entityType.FullName}_{permission}", policy =>
                policy.Requirements.Add(new EntityPermissionRequirement(permission)));
        }

        return options;
    }
}