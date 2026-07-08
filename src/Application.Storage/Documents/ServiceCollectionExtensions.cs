// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Storage;
using Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Provides service-collection extensions for registering document-store clients.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Starts a top-level fluent document-storage registration flow.
    /// </summary>
    /// <param name="services">The service collection to update.</param>
    /// <param name="configure">An optional callback used to configure document-storage registration.</param>
    /// <param name="configuration">The optional configuration root available to provider extensions.</param>
    /// <returns>The document-storage builder used to register clients and behaviors.</returns>
    /// <example>
    /// <code>
    /// services.AddDocumentStorage(o => o.Enabled(true))
    ///     .WithBehavior&lt;LoggingDocumentStoreClientBehavior&lt;Person&gt;&gt;()
    ///     .WithClient&lt;Person&gt;(sp => new DocumentStoreClient&lt;Person&gt;(
    ///         new InMemoryDocumentStoreProvider(sp.GetRequiredService&lt;ILoggerFactory&gt;())));
    /// </code>
    /// </example>
    public static DocumentStorageBuilderContext AddDocumentStorage(
        this IServiceCollection services,
        Action<DocumentStorageOptions> configure = null,
        IConfiguration configuration = null)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        var options = new DocumentStorageOptions();
        configure?.Invoke(options);

        if (options.IsEnabled)
        {
            services.TryAddSingleton(new DocumentStorageFeature { IsEnabled = true });
        }

        return new DocumentStorageBuilderContext(services, options, configuration);
    }

    /// <summary>
    /// Registers a custom document-store client within a top-level document-storage registration flow.
    /// </summary>
    /// <typeparam name="T">The document type handled by the client.</typeparam>
    /// <param name="context">The document-storage builder context.</param>
    /// <param name="clientFactory">The factory used to create the client implementation.</param>
    /// <param name="lifetime">The optional service lifetime override for this client.</param>
    /// <param name="capabilities">The optional provider capabilities used by dashboard selection and query safety hints.</param>
    /// <returns>The current document-storage builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddDocumentStorage()
    ///     .WithBehavior&lt;LoggingDocumentStoreClientBehavior&lt;Person&gt;&gt;()
    ///     .WithClient&lt;Person&gt;(sp => new DocumentStoreClient&lt;Person&gt;(
    ///         new InMemoryDocumentStoreProvider(sp.GetRequiredService&lt;ILoggerFactory&gt;())));
    /// </code>
    /// </example>
    public static DocumentStorageBuilderContext WithClient<T>(
        this DocumentStorageBuilderContext context,
        Func<IServiceProvider, IDocumentStoreClient<T>> clientFactory,
        ServiceLifetime? lifetime = null,
        DocumentStoreProviderCapabilities capabilities = null)
        where T : class, new()
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(clientFactory, nameof(clientFactory));

        if (!context.Options.IsEnabled)
        {
            return context;
        }

        context.Services.AddDocumentStoreClient(clientFactory, lifetime ?? context.Lifetime);

        return context.RegisterClient<T>("Custom", capabilities: capabilities);
    }

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
