// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Identity;

using Microsoft.AspNetCore.Authorization;

public static class IdentityOptionsBuilderExtensions
{
    /// <summary>
    /// Configures entity permissions for the identity system.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type that implements IEntityPermissionContext.</typeparam>
    /// <param name="source">The identity options builder.</param>
    /// <param name="configure">Action to configure entity permissions.</param>
    /// <returns>The identity options builder for chaining.</returns>
    /// <example>
    /// Configuring entity permissions in Program.cs:
    /// <code>
    /// services.AddIdentity(identity =>
    /// {
    ///     identity.WithEntityPermissions&lt;MyDbContext&gt;(permissions =>
    ///     {
    ///         permissions
    ///             .AddEntity&lt;Customer&gt;(Permission.Read, Permission.Write)
    ///             .AddHierarchicalEntity&lt;Department&gt;(d => d.ParentId, Permission.Read)
    ///             .EnableCaching();
    ///     });
    /// });
    /// </code>
    /// </example>
    public static IdentityOptionsBuilder WithEntityPermissions<TContext>(
        this IdentityOptionsBuilder source,
        Action<EntityPermissionOptionsBuilder<TContext>> configure)
        where TContext : DbContext, IEntityPermissionContext
    {
        var builder = new EntityPermissionOptionsBuilder<TContext>(source.Services);
        configure(builder);
        builder.Build();

        return source;
    }
}

public class EntityPermissionOptionsBuilder<TContext>(IServiceCollection services)
    where TContext : DbContext, IEntityPermissionContext
{
    private readonly EntityPermissionOptions options = new();

    /// <summary>
    /// Adds an entity type with specified permissions.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to configure.</typeparam>
    /// <param name="permissions">The permissions to enable for this entity.</param>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// <code>
    /// permissions.AddEntity&lt;Customer&gt;(Permission.Read, Permission.Write);
    /// </code>
    /// </example>
    public EntityPermissionOptionsBuilder<TContext> AddEntity<TEntity>(params Permission[] permissions)
        where TEntity : class, IEntity
    {
        this.options.AddEntity<TEntity>(permissions);
        return this;
    }

    /// <summary>
    /// Adds a hierarchical entity type with specified permissions.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to configure.</typeparam>
    /// <param name="parentIdExpression">Expression to the parent ID property.</param>
    /// <param name="permissions">The permissions to enable for this entity.</param>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// <code>
    /// permissions.AddHierarchicalEntity&lt;Department&gt;(
    ///     d => d.ParentDepartmentId,
    ///     Permission.Read,
    ///     Permission.Write);
    /// </code>
    /// </example>
    public EntityPermissionOptionsBuilder<TContext> AddHierarchicalEntity<TEntity>(
        Expression<Func<TEntity, object>> parentIdExpression, params Permission[] permissions)
        where TEntity : class, IEntity
    {
        this.options.AddHierarchicalEntity(parentIdExpression, permissions);
        return this;
    }

    /// <summary>
    /// Adds default permissions for an entity type.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to configure.</typeparam>
    /// <param name="permissions">The default permissions to grant.</param>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// <code>
    /// permissions.AddDefaultPermissions&lt;PublicDocument&gt;(Permission.Read);
    /// </code>
    /// </example>
    public EntityPermissionOptionsBuilder<TContext> AddDefaultPermissions<TEntity>(params Permission[] permissions)
        where TEntity : class, IEntity
    {
        this.options.AddDefaultPermissions<TEntity>(permissions);
        return this;
    }

    /// <summary>
    /// Configures a custom evaluator for an entity type.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TEValuator">The custom evaluator type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// <code>
    /// permissions.UseCustomEvaluator&lt;Customer, CustomCustomerPermissionEvaluator&gt;();
    /// </code>
    /// </example>
    public EntityPermissionOptionsBuilder<TContext> UseCustomEvaluator<TEntity, TEValuator>()
        where TEntity : class, IEntity
        where TEValuator : class, IEntityPermissionEvaluator<TEntity>
    {
        this.options.UseCustomEvaluator<TEntity, TEValuator>();
        return this;
    }

    /// <summary>
    /// Uses the default permission provider for an entity type, with permissions configured in the entity setup.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to configure default permissions for.</typeparam>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// Configuring default permissions using the configured entity permissions:
    /// <code>
    /// permissions
    ///     .AddEntity&lt;PublicDocument&gt;(Permission.Read, Permission.List)
    ///     .UseDefaultPermissionProvider&lt;PublicDocument&gt;();
    /// </code>
    /// </example>
    public EntityPermissionOptionsBuilder<TContext> UseDefaultPermissionProvider<TEntity>()
        where TEntity : class, IEntity
    {
        this.options.UseDefaultPermissionProvider<TEntity>();
        return this;
    }

    /// <summary>
    /// Uses a custom default permission provider for an entity type with optional explicit permissions.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to configure default permissions for.</typeparam>
    /// <typeparam name="TProvider">The custom default permission provider type.</typeparam>
    /// <param name="permissions">Optional set of explicit permissions. If null, permissions are determined by the provider.</param>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// Using a custom provider with explicit permissions:
    /// <code>
    /// permissions.UseDefaultPermissionProvider&lt;PublicDocument, PublicDocumentPermissionProvider&gt;(
    ///     new HashSet&lt;string&gt; { Permission.Read, Permission.List });
    /// </code>
    ///
    /// Using a custom provider with provider-defined permissions:
    /// <code>
    /// permissions.UseDefaultPermissionProvider&lt;PublicDocument, PublicDocumentPermissionProvider&gt;();
    /// </code>
    /// </example>
    public EntityPermissionOptionsBuilder<TContext> UseDefaultPermissionProvider<TEntity, TProvider>(HashSet<string> permissions = null)
        where TEntity : class, IEntity
        where TProvider : class, IDefaultEntityPermissionProvider<TEntity>
    {
        this.options.UseDefaultPermissionProvider<TEntity, TProvider>(permissions);
        return this;
    }

    /// <summary>
    /// Enables caching of permission results.
    /// </summary>
    /// <param name="value">Whether to enable caching (default: true).</param>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// <code>
    /// permissions
    ///     .EnableCaching()
    ///     .WithCacheLifetime(TimeSpan.FromMinutes(10));
    /// </code>
    /// </example>
    public EntityPermissionOptionsBuilder<TContext> EnableCaching(bool value = true)
    {
        this.options.EnableCaching = value;
        return this;
    }

    /// <summary>
    /// Sets the cache lifetime for permission results.
    /// </summary>
    /// <param name="value">The cache lifetime.</param>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// <code>
    /// permissions.WithCacheLifetime(TimeSpan.FromMinutes(5));
    /// </code>
    /// </example>
    public EntityPermissionOptionsBuilder<TContext> WithCacheLifetime(TimeSpan value)
    {
        this.options.CacheLifetime = value;
        return this;
    }

    public void Build()
    {
        services.AddSingleton(this.options);

        ConfigureServices(services, this.options);
    }

    private static void ConfigureServices(IServiceCollection services, EntityPermissionOptions options)
    {
        // Register core services
        services.AddSingleton(options);
        services.AddScoped<IEntityPermissionProvider, EntityFrameworkPermissionProvider<TContext>>();

        // Register AuthorizationHandler and its Evaluator for each configured entity type
        foreach (var entityConfig in options.EntityConfigurations)
        {
            // Register the authorization handler
            var handlerType1 = typeof(EntityPermissionInstanceAuthorizationHandler<>)
                .MakeGenericType(entityConfig.EntityType);
            services.AddScoped(typeof(IAuthorizationHandler), handlerType1);

            var handlerType2 = typeof(EntityPermissionTypeAuthorizationHandler<>)
                .MakeGenericType(entityConfig.EntityType);
            services.AddScoped(typeof(IAuthorizationHandler), handlerType2);

            // Register the evaluator
            var checkerInterface = typeof(IEntityPermissionEvaluator<>)
                .MakeGenericType(entityConfig.EntityType);

            if (options.EvaluatorConfigurations.TryGetValue(entityConfig.EntityType, out var evaluatorType))
            {
                services.AddScoped(checkerInterface, evaluatorType);
            }
            else
            {
                var defaultEvaluatorType = typeof(EntityPermissionEvaluator<>)
                    .MakeGenericType(entityConfig.EntityType);
                services.AddScoped(checkerInterface, defaultEvaluatorType);
            }
        }

        // Register default permission providers if configured
        foreach (var providerRegistration in options.DefaultPermissionProviders)
        {
            services.AddScoped(providerRegistration.InterfaceType, providerRegistration.ImplementationType);
        }

        if (options.EnableCaching) // Register caching if enabled
        {
            services.AddMemoryCache();

            if (!services.Any(d => d.ServiceType == typeof(ICacheProvider)))
            {
                services.AddSingleton<ICacheProvider, InMemoryCacheProvider>();
            }
        }

        // Register the authorization policies
        services.AddAuthorization(o =>
        {
            foreach (var entityConfig in options.EntityConfigurations)
            {
                if (entityConfig.Permissions?.Any() != true)
                {
                    continue;
                }

                o.AddEntityPermissionPolicy(entityConfig.EntityType, entityConfig.Permissions);
            }
        });
    }
}