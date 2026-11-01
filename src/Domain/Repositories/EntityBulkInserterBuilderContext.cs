// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Common;
using Configuration;
using Scrutor;

/// <summary>
/// Configures an <see cref="IEntityBulkInserter{TEntity}"/> and its ordered decorator behaviors.
/// </summary>
/// <typeparam name="TEntity">The entity type inserted by the configured bulk inserter.</typeparam>
/// <example>
/// <code>
/// services.AddEntityFrameworkBulkInserter&lt;Person, AppDbContext&gt;()
///     .WithBehavior&lt;AuditPersonBulkInserter&gt;()
///     .WithBehavior(inner => new ValidatePersonBulkInserter(inner))
///     .WithBehavior((inner, serviceProvider) =>
///         new LogPersonBulkInserter(inner, serviceProvider.GetRequiredService&lt;ILoggerFactory&gt;()));
/// </code>
/// </example>
public class EntityBulkInserterBuilderContext<TEntity>(
    IServiceCollection services,
    ServiceLifetime lifetime = ServiceLifetime.Scoped,
    IConfiguration configuration = null,
    Action<IServiceCollection, ServiceLifetime, Type> shadowValueProviderRegistration = null)
    where TEntity : class, IEntity
{
    private readonly List<Action<IServiceCollection>> behaviors = [];
    private readonly List<Action<IServiceCollection>> auditBehaviors = [];
    private readonly AuditBoundaryRegistrationCatalog auditRegistrations = DeclareAuditBoundaries(services);
    private bool auditingSelected;

    private ServiceDescriptor bulkInserterDescriptor;

    /// <summary>
    /// Gets the service collection that contains the bulk inserter registration.
    /// </summary>
    /// <example>
    /// <code>
    /// context.Services.AddSingleton&lt;ImportMetrics&gt;();
    /// </code>
    /// </example>
    public IServiceCollection Services { get; } = services;

    /// <summary>
    /// Gets the lifetime used by the terminal bulk inserter and its decorators.
    /// </summary>
    /// <example>
    /// <code>
    /// var lifetime = context.Lifetime;
    /// </code>
    /// </example>
    public ServiceLifetime Lifetime { get; } = lifetime;

    /// <summary>
    /// Gets the optional configuration associated with the bulk inserter registration.
    /// </summary>
    /// <example>
    /// <code>
    /// var configuration = context.Configuration;
    /// </code>
    /// </example>
    public IConfiguration Configuration { get; } = configuration;

    /// <summary>
    /// Adds a bulk inserter decorator resolved by its implementation type.
    /// </summary>
    /// <typeparam name="TBehavior">The bulk inserter decorator implementation type.</typeparam>
    /// <returns>The current builder context.</returns>
    /// <example>
    /// <code>
    /// context.WithBehavior&lt;LoggingPersonBulkInserter&gt;();
    /// </code>
    /// </example>
    public EntityBulkInserterBuilderContext<TEntity> WithBehavior<TBehavior>()
        where TBehavior : class, IEntityBulkInserter<TEntity>
    {
        if (typeof(TBehavior) == typeof(EntityBulkInserterAuditingBehavior<TEntity>)) return this.WithAuditing();
        this.AddBehavior<TBehavior>(services => services.Decorate<IEntityBulkInserter<TEntity>, TBehavior>());
        this.RegisterBehaviors();

        return this;
    }

    /// <summary>
    /// Adds a bulk inserter decorator created from the inner bulk inserter.
    /// </summary>
    /// <typeparam name="TBehavior">The bulk inserter decorator implementation type.</typeparam>
    /// <param name="behavior">Creates the decorator from its inner bulk inserter.</param>
    /// <returns>The current builder context.</returns>
    /// <example>
    /// <code>
    /// context.WithBehavior(inner => new ValidatePersonBulkInserter(inner));
    /// </code>
    /// </example>
    public EntityBulkInserterBuilderContext<TEntity> WithBehavior<TBehavior>(
        Func<IEntityBulkInserter<TEntity>, TBehavior> behavior)
        where TBehavior : notnull, IEntityBulkInserter<TEntity>
    {
        EnsureArg.IsNotNull(behavior, nameof(behavior));

        this.AddBehavior<TBehavior>(services =>
            services.Decorate<IEntityBulkInserter<TEntity>>((inner, _) => behavior(inner)));
        this.RegisterBehaviors();

        return this;
    }

    /// <summary>
    /// Adds a bulk inserter decorator created from the inner bulk inserter and service provider.
    /// </summary>
    /// <typeparam name="TBehavior">The bulk inserter decorator implementation type.</typeparam>
    /// <param name="behavior">Creates the decorator from its inner bulk inserter and service provider.</param>
    /// <returns>The current builder context.</returns>
    /// <example>
    /// <code>
    /// context.WithBehavior((inner, serviceProvider) =>
    ///     new LogPersonBulkInserter(inner, serviceProvider.GetRequiredService&lt;ILoggerFactory&gt;()));
    /// </code>
    /// </example>
    public EntityBulkInserterBuilderContext<TEntity> WithBehavior<TBehavior>(
        Func<IEntityBulkInserter<TEntity>, IServiceProvider, TBehavior> behavior)
        where TBehavior : notnull, IEntityBulkInserter<TEntity>
    {
        EnsureArg.IsNotNull(behavior, nameof(behavior));

        this.AddBehavior<TBehavior>(services =>
            services.Decorate<IEntityBulkInserter<TEntity>>((inner, serviceProvider) =>
                behavior(inner, serviceProvider)));
        this.RegisterBehaviors();

        return this;
    }

    /// <summary>
    /// Registers a provider-specific shadow-value provider through the configured bulk inserter registration callback.
    /// </summary>
    /// <typeparam name="TProvider">The shadow-value provider implementation type.</typeparam>
    /// <returns>The current builder context.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the registered bulk inserter implementation does not support shadow-value providers.
    /// </exception>
    /// <example>
    /// <code>
    /// services.AddEntityFrameworkBulkInserter&lt;Person, AppDbContext&gt;()
    ///     .WithShadowValueProvider&lt;TenantShadowValueProvider&gt;();
    /// </code>
    /// </example>
    public EntityBulkInserterBuilderContext<TEntity> WithShadowValueProvider<TProvider>()
        where TProvider : class
    {
        if (shadowValueProviderRegistration is null)
        {
            throw new InvalidOperationException(
                $"The bulk inserter registration for entity '{typeof(TEntity).FullName}' does not support shadow-value providers.");
        }

        shadowValueProviderRegistration(this.Services, this.Lifetime, typeof(TProvider));

        return this;
    }

    /// <summary>Selects whole-operation bulk insertion auditing without adding any baseline read.</summary>
    /// <example><code>bulk.WithAuditing();</code></example>
    public EntityBulkInserterBuilderContext<TEntity> WithAuditing()
    {
        if (this.auditingSelected) return this;
        this.auditingSelected = true;
        return this.WithBehavior<EntityBulkInserterAuditingBehavior<TEntity>>((inner, services) => this.Lifetime == ServiceLifetime.Singleton
            ? new(inner, scopes: services.GetService<IServiceProviderIsService>()?.IsService(typeof(IAuditContext)) == true ? services.GetRequiredService<IServiceScopeFactory>() : null)
            : new(inner, services.GetService<IAuditContext>()));
    }

    private static AuditBoundaryRegistrationCatalog DeclareAuditBoundaries(IServiceCollection services)
    {
        var catalog = new AuditBoundaryRegistrationCatalog("BulkInserter", "Insert");
        catalog.Select("Insert", "omitted", typeof(TEntity).Name);
        services.AddSingleton<IAuditBoundaryRegistrationSource>(catalog);
        return catalog;
    }
    private void AddBehavior<TBehavior>(Action<IServiceCollection> register)
    {
        if (typeof(TBehavior) == typeof(EntityBulkInserterAuditingBehavior<TEntity>))
        {
            if (this.auditBehaviors.Count > 0) return;
            this.auditBehaviors.Add(register);
            this.auditRegistrations.Select("Insert", typeAlias: typeof(TEntity).Name);
        }
        else this.behaviors.Add(register);
    }

    private IServiceCollection RegisterBehaviors()
    {
        this.bulkInserterDescriptor ??= this.Services.Find<IEntityBulkInserter<TEntity>>();
        if (this.bulkInserterDescriptor is null)
        {
            throw new InvalidOperationException(
                $"Cannot register behaviors for {typeof(IEntityBulkInserter<TEntity>).PrettyName()} before the terminal bulk inserter is registered.");
        }

        var descriptorIndex = this.Services.IndexOf<IEntityBulkInserter<TEntity>>();
        if (descriptorIndex is -1)
        {
            return this.Services;
        }

        this.Services[descriptorIndex] = this.bulkInserterDescriptor;

        foreach (var descriptor in this.Services
                     .Where(descriptor =>
                         descriptor.ServiceType is DecoratedType &&
                         descriptor.ServiceType.ImplementsInterface(typeof(IEntityBulkInserter<TEntity>)))
                     .ToList())
        {
            this.Services.Remove(descriptor);
        }

        foreach (var behavior in this.behaviors.AsEnumerable().Reverse().Concat(this.auditBehaviors.AsEnumerable().Reverse()))
        {
            behavior(this.Services);
        }

        return this.Services;
    }
}
