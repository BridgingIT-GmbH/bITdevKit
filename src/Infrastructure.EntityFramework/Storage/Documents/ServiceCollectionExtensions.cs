// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;
using Microsoft.Extensions.Logging;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers an Entity Framework backed document-store client for <typeparamref name="T" />.
    /// </summary>
    /// <typeparam name="T">The document payload type.</typeparam>
    /// <typeparam name="TContext">The EF Core context that implements <see cref="IDocumentStoreContext" />.</typeparam>
    /// <param name="services">The service collection to update.</param>
    /// <param name="provider">An optional pre-built provider instance.</param>
    /// <param name="lifetime">The client lifetime to register.</param>
    /// <param name="configure">An optional callback used to customize provider lease and retry options.</param>
    /// <returns>The document-store builder for adding behaviors.</returns>
    /// <example>
    /// <code>
    /// services.AddDbContext&lt;AppDbContext&gt;(options => options.UseSqlServer(connectionString));
    ///
    /// services.AddEntityFrameworkDocumentStoreClient&lt;Person, AppDbContext&gt;(
    ///         lifetime: ServiceLifetime.Singleton,
    ///         configure: options =>
    ///         {
    ///             options.LeaseDuration = TimeSpan.FromSeconds(15);
    ///             options.RetryCount = 5;
    ///         })
    ///     .WithBehavior&lt;LoggingDocumentStoreClientBehavior&lt;Person&gt;&gt;();
    /// </code>
    /// </example>
    public static DocumentStoreBuilderContext<T> AddEntityFrameworkDocumentStoreClient<T, TContext>(
        this IServiceCollection services,
        EntityFrameworkDocumentStoreProvider<TContext> provider = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped,
        Action<EntityFrameworkDocumentStoreProviderOptions> configure = null)
        where T : class, new()
        where TContext : DbContext, IDocumentStoreContext
    {
        EnsureArg.IsNotNull(services, nameof(services));

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton<IDocumentStoreClient<T>>(sp => new DocumentStoreClient<T>(
                    provider ?? CreateProvider<TContext>(sp, configure)));

                break;
            case ServiceLifetime.Transient:
                services.AddTransient<IDocumentStoreClient<T>>(sp => new DocumentStoreClient<T>(
                    provider ?? CreateProvider<TContext>(sp, configure)));

                break;
            default:
                services.AddScoped<IDocumentStoreClient<T>>(sp => new DocumentStoreClient<T>(
                    provider ?? CreateProvider<TContext>(sp, configure)));

                break;
        }

        return new DocumentStoreBuilderContext<T>(services, lifetime);
    }

    private static EntityFrameworkDocumentStoreProvider<TContext> CreateProvider<TContext>(
        IServiceProvider serviceProvider,
        Action<EntityFrameworkDocumentStoreProviderOptions> configure)
        where TContext : DbContext, IDocumentStoreContext
    {
        var options = new EntityFrameworkDocumentStoreProviderOptions
        {
            LoggerFactory = serviceProvider.GetService<ILoggerFactory>()
        };
        configure?.Invoke(options);

        return new EntityFrameworkDocumentStoreProvider<TContext>(
            serviceProvider,
            serviceProvider.GetService<ILoggerFactory>(),
            options: options);
    }
}
