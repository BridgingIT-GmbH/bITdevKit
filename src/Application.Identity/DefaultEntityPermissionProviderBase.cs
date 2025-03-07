// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Identity;

using BridgingIT.DevKit.Domain.Model;

/// <summary>
/// Base implementation of a default permission provider that allows easy configuration of permissions.
/// </summary>
/// <typeparam name="TEntity">The type of entity for which default permissions are provided.</typeparam>
public abstract class DefaultEntityPermissionProviderBase<TEntity> : IDefaultEntityPermissionProvider<TEntity>
    where TEntity : class, IEntity
{
    private readonly HashSet<string> permissions = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultEntityPermissionProviderBase{TEntity}"/> class.
    /// </summary>
    protected DefaultEntityPermissionProviderBase()
    {
        this.ConfigurePermissions(new DefaultPermissionConfiguration(this.permissions));
    }

    /// <summary>
    /// Configures the default permissions for the entity type.
    /// Must be implemented by derived classes to define the default permissions.
    /// </summary>
    /// <param name="config">The configuration object to use for setting up permissions.</param>
    protected abstract void ConfigurePermissions(DefaultPermissionConfiguration config);

    /// <inheritdoc/>
    public HashSet<string> GetDefaultPermissions() => this.permissions ?? [];

    /// <summary>
    /// Configuration class for setting up default permissions.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="DefaultPermissionConfiguration"/> class.
    /// </remarks>
    /// <param name="permissions">The set of permissions to configure.</param>
    protected class DefaultPermissionConfiguration(HashSet<string> permissions)
    {
        private readonly HashSet<string> permissions = permissions;

        /// <summary>
        /// Adds a permission to the default set.
        /// </summary>
        /// <param name="permission">The permission to add.</param>
        /// <returns>The configuration object for method chaining.</returns>
        public DefaultPermissionConfiguration AddPermission(string permission)
        {
            this.permissions.Add(permission);
            return this;
        }

        /// <summary>
        /// Adds multiple permissions to the default set.
        /// </summary>
        /// <param name="permissions">The permissions to add.</param>
        /// <returns>The configuration object for method chaining.</returns>
        public DefaultPermissionConfiguration AddPermissions(params string[] permissions)
        {
            foreach (var permission in permissions)
            {
                this.permissions.Add(permission);
            }
            return this;
        }
    }
}