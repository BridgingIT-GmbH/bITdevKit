// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Entities;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Registers ChangeHistory services and options.
/// </summary>
/// <example>
/// <code>
/// services.AddChangeHistory(options =&gt; options
///     .Track&lt;Customer&gt;()
///         .CaptureDirectMutations(ChangeHistoryCaptureStrategy.RepositorySnapshot))
///     .WithReadAuthorizer&lt;AppDbContext, AppChangeHistoryReadAuthorizer&gt;();
/// </code>
/// </example>
public static class ChangeHistoryServiceCollectionExtensions
{
    /// <summary>
    /// Registers ChangeHistory options for repository behaviors.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The options configuration callback.</param>
    /// <returns>The ChangeHistory builder context for chaining registrations.</returns>
    public static ChangeHistoryBuilderContext AddChangeHistory(
        this IServiceCollection services,
        Action<ChangeHistoryOptions> configure = null)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        var options = new ChangeHistoryOptions();
        configure?.Invoke(options);
        options.Validate();
        services.AddSingleton(options);

        foreach (var entityOptions in options.TrackedEntities.Values)
        {
            if (entityOptions.RestoreAuthorizerType is not null)
            {
                services.TryAddScoped(entityOptions.RestoreAuthorizerType);
            }
        }

        return new ChangeHistoryBuilderContext(services, options);
    }

    /// <summary>
    /// Registers the ChangeHistory read authorizer for one EF Core context.
    /// </summary>
    /// <typeparam name="TContext">The EF Core context that stores ChangeHistory rows.</typeparam>
    /// <typeparam name="TAuthorizer">The read authorizer implementation.</typeparam>
    /// <param name="context">The ChangeHistory builder context.</param>
    /// <returns>The same ChangeHistory builder context for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddChangeHistory()
    ///     .WithReadAuthorizer&lt;AppDbContext, AppChangeHistoryReadAuthorizer&gt;();
    /// </code>
    /// </example>
    public static ChangeHistoryBuilderContext WithReadAuthorizer<TContext, TAuthorizer>(
        this ChangeHistoryBuilderContext context)
        where TContext : DbContext
        where TAuthorizer : class, IChangeHistoryReadAuthorizer<TContext>
    {
        EnsureArg.IsNotNull(context, nameof(context));

        context.Services.AddScoped<IChangeHistoryReadAuthorizer<TContext>, TAuthorizer>();

        return context;
    }

    /// <summary>
    /// Registers the ChangeHistory restore-request authorizer for one entity and EF Core context.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being restored.</typeparam>
    /// <typeparam name="TContext">The EF Core context that stores ChangeHistory rows.</typeparam>
    /// <typeparam name="TAuthorizer">The restore-request authorizer implementation.</typeparam>
    /// <param name="context">The ChangeHistory builder context.</param>
    /// <returns>The same ChangeHistory builder context for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddChangeHistory(options =&gt; options.Track&lt;Customer&gt;())
    ///     .WithRestoreRequestAuthorizer&lt;Customer, AppDbContext, CustomerChangeHistoryRestoreRequestAuthorizer&gt;();
    /// </code>
    /// </example>
    public static ChangeHistoryBuilderContext WithRestoreRequestAuthorizer<TEntity, TContext, TAuthorizer>(
        this ChangeHistoryBuilderContext context)
        where TEntity : class, IEntity
        where TContext : DbContext
        where TAuthorizer : class, IChangeHistoryRestoreRequestAuthorizer<TEntity, TContext>
    {
        EnsureArg.IsNotNull(context, nameof(context));

        context.Services.AddScoped<IChangeHistoryRestoreRequestAuthorizer<TEntity, TContext>, TAuthorizer>();

        return context;
    }

    /// <summary>
    /// Registers ChangeHistory query services for one EF Core context.
    /// </summary>
    /// <typeparam name="TContext">The EF Core context that stores ChangeHistory rows.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddChangeHistoryServices&lt;AppDbContext&gt;();
    /// </code>
    /// </example>
    public static IServiceCollection AddChangeHistoryServices<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        EnsureArg.IsNotNull(services, nameof(services));

        services.TryAddScoped<ChangeHistoryQueryService<TContext>>();

        return services;
    }

    /// <summary>
    /// Registers ChangeHistory read and restore services for one entity and EF Core context.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to restore.</typeparam>
    /// <typeparam name="TContext">The EF Core context that stores ChangeHistory rows.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddChangeHistoryServices&lt;Customer, AppDbContext&gt;();
    /// </code>
    /// </example>
    public static IServiceCollection AddChangeHistoryServices<TEntity, TContext>(this IServiceCollection services)
        where TEntity : class, IEntity
        where TContext : DbContext
    {
        EnsureArg.IsNotNull(services, nameof(services));

        services.AddChangeHistoryServices<TContext>();
        services.TryAddScoped<ChangeHistoryRestoreCommandHandler<TEntity, TContext>>();
        services.TryAddScoped<IChangeHistoryService<TEntity, TContext>, ChangeHistoryService<TEntity, TContext>>();

        return services;
    }

    /// <summary>
    /// Registers requester handlers for querying ChangeHistory rows for one EF Core context.
    /// </summary>
    /// <typeparam name="TContext">The EF Core context that stores ChangeHistory rows.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddRequester();
    /// services.AddChangeHistoryRequesterHandlers&lt;AppDbContext&gt;();
    /// </code>
    /// </example>
    public static IServiceCollection AddChangeHistoryRequesterHandlers<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        EnsureArg.IsNotNull(services, nameof(services));

        services.AddChangeHistoryServices<TContext>();
        services.TryAddScoped<IRequestHandler<ChangeHistoryFindAllRequest<TContext>, ChangeHistoryFindAllResult>, ChangeHistoryFindAllRequestHandler<TContext>>();
        services.TryAddScoped<IRequestHandler<ChangeHistoryFindAllChangeSetsRequest<TContext>, ChangeHistoryFindAllChangeSetsResult>, ChangeHistoryFindAllChangeSetsRequestHandler<TContext>>();
        services.TryAddScoped<IRequestHandler<ChangeHistoryFindOneChangeSetRequest<TContext>, ChangeHistoryChangeSetRecord>, ChangeHistoryFindOneChangeSetRequestHandler<TContext>>();

        return services;
    }

    /// <summary>
    /// Registers requester handlers for querying and restoring ChangeHistory for one entity and EF Core context.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to restore.</typeparam>
    /// <typeparam name="TContext">The EF Core context that stores ChangeHistory rows.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddRequester();
    /// services.AddChangeHistoryRequesterHandlers&lt;Customer, AppDbContext&gt;();
    /// </code>
    /// </example>
    public static IServiceCollection AddChangeHistoryRequesterHandlers<TEntity, TContext>(this IServiceCollection services)
        where TEntity : class, IEntity
        where TContext : DbContext
    {
        EnsureArg.IsNotNull(services, nameof(services));

        services.AddChangeHistoryServices<TEntity, TContext>();
        services.TryAddScoped<IRequestHandler<ChangeHistoryRestoreRequest<TEntity, TContext>, ChangeHistoryRestoreResult>, ChangeHistoryRestoreRequestHandler<TEntity, TContext>>();

        return services;
    }
}
