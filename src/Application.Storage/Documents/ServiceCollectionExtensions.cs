// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides service-collection extensions for registering document-store clients.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers an <see cref="IDocumentStoreClient{T}" /> using a custom factory and returns a fluent builder for behaviors.
    /// </summary>
    /// <typeparam name="T">The document type handled by the client.</typeparam>
    /// <param name="services">The service collection to update.</param>
    /// <param name="clientFactory">The factory used to create the client implementation.</param>
    /// <param name="lifetime">The service lifetime to use for the registration.</param>
    /// <returns>A builder that can be used to add client behaviors.</returns>
    /// <example>
    /// <code>
    /// services.AddDocumentStoreClient&lt;Person&gt;(
    ///         sp => new DocumentStoreClient&lt;Person&gt;(
    ///             new InMemoryDocumentStoreProvider(sp.GetRequiredService&lt;ILoggerFactory&gt;())))
    ///     .WithBehavior&lt;LoggingDocumentStoreClientBehavior&lt;Person&gt;&gt;();
    /// </code>
    /// </example>
    public static DocumentStoreBuilderContext<T> AddDocumentStoreClient<T>(
        this IServiceCollection services,
        Func<IServiceProvider, IDocumentStoreClient<T>> clientFactory,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where T : class, new()
    {
        EnsureArg.IsNotNull(services, nameof(services));
        EnsureArg.IsNotNull(clientFactory, nameof(clientFactory));

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton(typeof(IDocumentStoreClient<T>), clientFactory);

                break;
            case ServiceLifetime.Transient:
                services.AddTransient(typeof(IDocumentStoreClient<T>), clientFactory);

                break;
            default:
                services.AddScoped(typeof(IDocumentStoreClient<T>), clientFactory);

                break;
        }

        return new DocumentStoreBuilderContext<T>(services, lifetime);
    }
}
