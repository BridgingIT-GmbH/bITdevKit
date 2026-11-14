// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Creates fresh caller-owned SQL Server contexts from independent immutable options and singleton-compatible host services.</summary>
/// <typeparam name="TContext">The context with an options constructor and optional singleton-compatible dependencies.</typeparam>
/// <param name="services">The host services used only to activate constructor dependencies.</param>
/// <param name="options">The independently configured native options, separate from scoped business-context options.</param>
/// <remarks>Creation never opens a database connection. Every returned context is unpooled and must be disposed by its caller. Context constructors must not require request-scoped services from this singleton factory.</remarks>
/// <exception cref="ArgumentNullException">The host services or independent options are null.</exception>
/// <example><code>await using var context = await factory.CreateDbContextAsync(cancellationToken);</code></example>
public sealed class SqlServerDbContextFactory<TContext>(IServiceProvider services, DbContextOptions<TContext> options) : IDbContextFactory<TContext>
    where TContext : DbContext
{
    private static readonly ObjectFactory ContextActivator = ActivatorUtilities.CreateFactory(typeof(TContext), [typeof(DbContextOptions<TContext>)]);
    private readonly IServiceProvider services = services ?? throw new ArgumentNullException(nameof(services));
    private readonly DbContextOptions<TContext> options = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>Creates one fresh context without resolving the scoped DbContext or scoped options registration.</summary>
    /// <returns>The independently owned context, which the caller must dispose.</returns>
    /// <exception cref="InvalidOperationException">A required context constructor dependency cannot be resolved from the factory's host services.</exception>
    /// <example><code>using var context = factory.CreateDbContext();</code></example>
    public TContext CreateDbContext() => (TContext)ContextActivator(this.services, [this.options]);

    /// <summary>Creates one fresh context after honoring pre-cancellation; context activation performs no asynchronous database work.</summary>
    /// <param name="cancellationToken">Cancellation checked before activating any context or dependency.</param>
    /// <returns>The independently owned context, or a canceled task when cancellation was already requested.</returns>
    /// <example><code>await using var context = await factory.CreateDbContextAsync(cancellationToken);</code></example>
    public Task<TContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => cancellationToken.IsCancellationRequested ?
        Task.FromCanceled<TContext>(cancellationToken) : Task.FromResult(this.CreateDbContext());
}