// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Identity;

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;

/// <summary>
/// Configuration options for entity permission services.
/// </summary>
public class EntityPermissionOptions
{
    /// <summary>
    /// Gets the evaluator configurations.
    /// </summary>
    public Dictionary<Type, Type> EvaluatorConfigurations { get; } = [];

    /// <summary>
    /// Gets the entity configurations.
    /// </summary>
    public List<EntityTypeConfiguration> EntityConfigurations { get; } = [];

    /// <summary>
    /// Gets the default entity permissions.
    /// </summary>
    public Dictionary<Type, HashSet<string>> DefaultEntityPermissions { get; } = [];

    /// <summary>
    /// Gets the default permission providers.
    /// </summary>
    public List<DefaultPermissionProviderRegistration> DefaultPermissionProviders { get; } = [];

    /// <summary>
    /// Gets or sets whether permission caching is enabled.
    /// </summary>
    public bool EnableCaching { get; set; }

    /// <summary>
    /// Gets or sets the sliding expiration timespan for cached permissions.
    /// Default is 5 minutes.
    /// </summary>
    public TimeSpan CacheLifetime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets whether hierarchical permission inheritance is enabled.
    /// When true, permissions are inherited from parent entities for types implementing IHierarchicalEntity.
    /// </summary>
    public bool EnableHierarchicalPermissions { get; set; } = true;

    //public bool EnableEvaluationEndpoints { get; set; }

    //public bool EnableManagementEndpoints { get; set; }

    public EntityPermissionOptions AddEntity<TEntity>(params Permission[] permissions)
        where TEntity : class, IEntity
    {
        var entityType = typeof(TEntity);

        if (this.EntityConfigurations.SafeAny(c => c.EntityType == entityType))
        {
            throw new InvalidOperationException($"Entity type {entityType.Name} already configured");
        }

        var idPropertyType = GetEntityIdPropertyType<TEntity>();

        this.EntityConfigurations.Add(
            new EntityTypeConfiguration(entityType, idPropertyType, permissions));

        return this;
    }

    /// <summary>
    /// Configures hierarchical permissions for an entity type.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="parentIdExpression">The expression to get the parent ID. The property type must match the entity ID type,
    /// but can be nullable.</param>
    /// <returns>The options instance for chaining.</returns>
    public EntityPermissionOptions AddHierarchicalEntity<TEntity>(
        Expression<Func<TEntity, object>> parentIdExpression, params Permission[] permissions)
        where TEntity : class, IEntity
    {
        var entityType = typeof(TEntity);

        if (this.EntityConfigurations.SafeAny(c => c.EntityType == typeof(TEntity)))
        {
            throw new InvalidOperationException(
                $"Entity type {typeof(TEntity).Name} allready configured");
        }

        // Handle both direct member access and conversions
        var memberExpression = parentIdExpression.Body as MemberExpression;
        if (memberExpression == null)
        {
            // If body is a convert expression (due to boxing to object), get the operand
            if (parentIdExpression.Body is UnaryExpression unaryExpression)
            {
                memberExpression = unaryExpression.Operand as MemberExpression;
            }
        }

        if (memberExpression == null)
        {
            throw new ArgumentException("Expression must be a member access", nameof(parentIdExpression));
        }

        // Verify that parent ID type matches entity ID type
        var idPropertyType = GetEntityIdPropertyType<TEntity>();
        var parentProperty = memberExpression.Member as PropertyInfo;
        var parentPropertyType = parentProperty.PropertyType;

        // Get the underlying type if it's nullable
        if (idPropertyType != (Nullable.GetUnderlyingType(parentPropertyType) ?? parentPropertyType))
        {
            throw new ArgumentException($"Parent ID type must match entity ID type (nullable allowed) for type {entityType.Name}");
        }

        this.EntityConfigurations.Add(
            new EntityTypeConfiguration(entityType, idPropertyType, permissions, parentProperty));

        return this;
    }

    /// <summary>
    /// Adds default permissions for a specific entity type.
    /// </summary>
    /// <typeparam name="TEntity">The type of entity.</typeparam>
    /// <param name="permissions">The permissions to add as defaults.</param>
    /// <returns>The options instance for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the entity type is not configured.</exception>
    public EntityPermissionOptions AddDefaultPermissions<TEntity>(params Permission[] permissions)
        where TEntity : class, IEntity
    {
        if (!this.EntityConfigurations.SafeAny(c => c.EntityType == typeof(TEntity)))
        {
            throw new InvalidOperationException(
                $"Entity type {typeof(TEntity).Name} must be configured using AddEntity before adding default permissions");
        }

        if (!this.DefaultEntityPermissions.TryGetValue(typeof(TEntity), out var entityPermissions))
        {
            entityPermissions = [];
            this.DefaultEntityPermissions[typeof(TEntity)] = entityPermissions;
        }

        foreach (var permission in permissions)
        {
            entityPermissions.Add(permission);
        }

        return this;
    }

    /// <summary>
    /// Configures a custom permission evaluator implementation for an entity type.
    /// </summary>
    /// <typeparam name="TEntity">The type of entity.</typeparam>
    /// <typeparam name="TEValuator">The type of the custom permission evaluator.</typeparam>
    /// <returns>The options instance for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the entity type is not configured.</exception>
    public EntityPermissionOptions UseCustomEvaluator<TEntity, TEValuator>()
        where TEntity : class, IEntity
        where TEValuator : class, IEntityPermissionEvaluator<TEntity>
    {
        if (!this.EntityConfigurations.SafeAny(e => e.EntityType == typeof(TEntity)))
        {
            throw new InvalidOperationException($"Entity type {typeof(TEntity).Name} must be configured using AddEntity before adding a custom evaluator");
        }

        this.EvaluatorConfigurations[typeof(TEntity)] = typeof(TEValuator);

        return this;
    }

    /// <summary>
    /// Creates a default permission provider for the specified entity type.
    /// </summary>
    /// <typeparam name="TEntity">The type of entity.</typeparam>
    /// <returns>The options instance for chaining.</returns>
    public EntityPermissionOptions UseDefaultPermissionProvider<TEntity>()
        where TEntity : class, IEntity
    {
        if (!this.DefaultEntityPermissions.TryGetValue(typeof(TEntity), out var permissions))
        {
            return this;
        }

        this.DefaultPermissionProviders.Add(
            new DefaultPermissionProviderRegistration(
                InterfaceType: typeof(IDefaultEntityPermissionProvider<>).MakeGenericType(typeof(TEntity)),
                ImplementationType: typeof(DefaultPermissionProvider<>).MakeGenericType(typeof(TEntity)),
                permissions)
            );

        return this;
    }

    /// <summary>
    /// Adds a default permission provider for an entity type.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TProvider">The type of the default permission provider.</typeparam>
    /// <returns>The options instance for chaining.</returns>
    public EntityPermissionOptions UseDefaultPermissionProvider<TEntity, TProvider>(HashSet<string> permissions = null)
        where TProvider : class, IDefaultEntityPermissionProvider<TEntity>
        where TEntity : class, IEntity
    {
        var interfaceType = typeof(IDefaultEntityPermissionProvider<TEntity>);
        var implementationType = typeof(TProvider);

        this.DefaultPermissionProviders.Add(
            new DefaultPermissionProviderRegistration(interfaceType, implementationType, permissions));

        return this;
    }

    /// <summary>
    /// Gets the entity type configuration for a specific entity type.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <returns>The entity type configuration.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the entity type is not configured.</exception>
    public EntityTypeConfiguration GetEntityTypeConfiguration<TEntity>()
        where TEntity : class, IEntity
    {
        if (!this.EntityConfigurations.SafeAny(e => e.EntityType == typeof(TEntity)))
        {
            throw new InvalidOperationException($"Entity type {typeof(TEntity).Name} not valid");
        }

        return this.EntityConfigurations.First(e => e.EntityType == typeof(TEntity));
    }

    /// <summary>
    /// Gets the entity type configuration for a specific entity type.
    /// </summary>
    /// <param name="entityType">The entity type.</param>
    /// <param name="throwNotFound"></param>
    /// <returns>The entity type configuration.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the entity type is not configured.</exception>
    public EntityTypeConfiguration GetEntityTypeConfiguration(Type entityType, bool throwNotFound = true)
    {
        var entityConfiguration = this.EntityConfigurations.FirstOrDefault(e => e.EntityType == entityType);

        if (throwNotFound && entityConfiguration == null)
        {
            throw new InvalidOperationException($"Entity type {entityType.Name} not valid");
        }

        return entityConfiguration;
    }

    /// <summary>
    /// Gets the entity type configuration for a specific entity type.
    /// </summary>
    /// <param name="entityType">The entity type.</param>
    /// <param name="throwNotFound"></param>
    /// <returns>The entity type configuration.</returns>
    public EntityTypeConfiguration GetEntityTypeConfiguration(string entityType, bool throwNotFound = true)
    {
        var entityConfiguration = this.EntityConfigurations.FirstOrDefault(e =>
            e.EntityType.FullName.SafeEquals(entityType) || e.EntityType.Name.SafeEquals(entityType));

        if (throwNotFound && entityConfiguration == null)
        {
            throw new InvalidOperationException($"Entity type {entityType} not valid");
        }

        return entityConfiguration;
    }

    private static Type GetEntityIdPropertyType<TEntity>() where TEntity : class, IEntity
    {
        var entityType = typeof(TEntity);
        var hierarchy = new List<Type> { entityType };
        var currentType = entityType;

        while (currentType.BaseType != null)
        {
            hierarchy.Add(currentType.BaseType);
            currentType = currentType.BaseType;
        }

        foreach (var type in hierarchy)
        {
            var idProperty = type.GetProperty("Id",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            if (idProperty != null)
            {
                return idProperty.PropertyType;
            }
        }

        throw new InvalidOperationException($"Could not determine Id property for type {entityType.Name}");
    }
}

/// <summary>
/// Represents the configuration for an entity type.
/// </summary>
public record EntityTypeConfiguration(Type EntityType, Type IdType, Permission[] Permissions, PropertyInfo ParentIdProperty = null)
{
    /// <summary>
    /// Gets a value indicating whether the entity type is hierarchical.
    /// </summary>
    public bool IsHierarchical => this.ParentIdProperty != null;
}

/// <summary>
/// Represents the registration of a default permission provider.
/// </summary>
public record DefaultPermissionProviderRegistration(Type InterfaceType, Type ImplementationType, HashSet<string> Permissions = null);