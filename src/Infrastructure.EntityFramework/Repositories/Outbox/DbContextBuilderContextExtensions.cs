// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Domain.Outbox;

/// <summary>
/// Provides builder extensions for registering the Entity Framework backed domain event outbox service.
/// </summary>
public static partial class DbContextBuilderContextExtensions
{
    /// <summary>
    /// Registers the domain event outbox service for the current DbContext builder using a fluent options builder.
    /// </summary>
    /// <typeparam name="TContext">The database context type that implements <see cref="IOutboxDomainEventContext" />.</typeparam>
    /// <param name="context">The DbContext builder context.</param>
    /// <param name="optionsBuilder">The fluent options builder used to customize outbox processing.</param>
    /// <returns>The current <see cref="DbContextBuilderContext{TContext}" /> for further composition.</returns>
    /// <example>
    /// <code>
    /// builder.WithOutboxDomainEventService&lt;AppDbContext&gt;(options => options
    ///     .ProcessingInterval(TimeSpan.FromSeconds(10))
    ///     .LeaseDuration(TimeSpan.FromSeconds(30))
    ///     .LeaseRenewalInterval(TimeSpan.FromSeconds(10)));
    /// </code>
    /// </example>
    public static DbContextBuilderContext<TContext> WithOutboxDomainEventService<TContext>(
        this DbContextBuilderContext<TContext> context,
        Builder<OutboxDomainEventOptionsBuilder, OutboxDomainEventOptions> optionsBuilder)
        where TContext : DbContext, IOutboxDomainEventContext
    {
        context.Services.AddOutboxDomainEventService<TContext>(optionsBuilder);

        return context;
    }

    /// <summary>
    /// Registers the domain event outbox service for the current DbContext builder using a concrete options instance.
    /// </summary>
    /// <typeparam name="TContext">The database context type that implements <see cref="IOutboxDomainEventContext" />.</typeparam>
    /// <param name="context">The DbContext builder context.</param>
    /// <param name="options">The outbox processing options.</param>
    /// <returns>The current <see cref="DbContextBuilderContext{TContext}" /> for further composition.</returns>
    /// <example>
    /// <code>
    /// builder.WithOutboxDomainEventService&lt;AppDbContext&gt;(new OutboxDomainEventOptions
    /// {
    ///     ProcessingInterval = TimeSpan.FromSeconds(10),
    ///     LeaseDuration = TimeSpan.FromSeconds(30)
    /// });
    /// </code>
    /// </example>
    public static DbContextBuilderContext<TContext> WithOutboxDomainEventService<TContext>(
        this DbContextBuilderContext<TContext> context,
        OutboxDomainEventOptions options = null)
        where TContext : DbContext, IOutboxDomainEventContext
    {
        context.Services.AddOutboxDomainEventService<TContext>(options);

        return context;
    }
}
