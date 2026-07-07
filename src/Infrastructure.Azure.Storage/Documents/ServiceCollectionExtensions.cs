// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Infrastructure.Azure;

/// <summary>
/// Provides Azure Storage document-store service registration extensions.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers an Azure Blob Storage backed document-store client for <typeparamref name="T" />.
    /// </summary>
    /// <typeparam name="T">The document payload type.</typeparam>
    /// <param name="services">The service collection to update.</param>
    /// <param name="provider">An optional pre-built Azure Blob provider.</param>
    /// <param name="lifetime">The client lifetime to register.</param>
    /// <param name="documentStoreOptions">The optional document-store query safety options.</param>
    /// <returns>The document-store builder for adding behaviors.</returns>
    /// <example>
    /// <code>
    /// services.AddAzureBlobDocumentStoreClient&lt;Person&gt;(
    ///     lifetime: ServiceLifetime.Scoped,
    ///     documentStoreOptions: new DocumentStoreOptions { MaxTake = 500 });
    /// </code>
    /// </example>
    public static DocumentStoreBuilderContext<T> AddAzureBlobDocumentStoreClient<T>(
        this IServiceCollection services,
        AzureBlobDocumentStoreProvider provider = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped,
        DocumentStoreOptions documentStoreOptions = null)
        where T : class, new()
    {
        EnsureArg.IsNotNull(services, nameof(services));

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton<IDocumentStoreClient<T>>(sp => new DocumentStoreClient<T>(provider ??
                    new AzureBlobDocumentStoreProvider(sp.GetRequiredService<ILoggerFactory>(),
                        sp.GetRequiredService<BlobServiceClient>(),
                        options: documentStoreOptions)));

                break;
            case ServiceLifetime.Transient:
                services.AddTransient<IDocumentStoreClient<T>>(sp => new DocumentStoreClient<T>(provider ??
                    new AzureBlobDocumentStoreProvider(sp.GetRequiredService<ILoggerFactory>(),
                        sp.GetRequiredService<BlobServiceClient>(),
                        options: documentStoreOptions)));

                break;
            default:
                services.AddScoped<IDocumentStoreClient<T>>(sp => new DocumentStoreClient<T>(provider ??
                    new AzureBlobDocumentStoreProvider(sp.GetRequiredService<ILoggerFactory>(),
                        sp.GetRequiredService<BlobServiceClient>(),
                        options: documentStoreOptions)));

                break;
        }

        return new DocumentStoreBuilderContext<T>(services, lifetime);
    }

    /// <summary>
    /// Registers an Azure Blob Storage backed document-store client for <typeparamref name="T" /> using a blob service client.
    /// </summary>
    /// <typeparam name="T">The document payload type.</typeparam>
    /// <param name="services">The service collection to update.</param>
    /// <param name="serviceClient">The optional blob service client. When null, the client is resolved from services.</param>
    /// <param name="lifetime">The client lifetime to register.</param>
    /// <param name="documentStoreOptions">The optional document-store query safety options.</param>
    /// <returns>The document-store builder for adding behaviors.</returns>
    /// <example>
    /// <code>
    /// services.AddAzureBlobDocumentStoreClient&lt;Person&gt;(
    ///     blobServiceClient,
    ///     documentStoreOptions: new DocumentStoreOptions { DefaultTake = 100 });
    /// </code>
    /// </example>
    public static DocumentStoreBuilderContext<T> AddAzureBlobDocumentStoreClient<T>(
        this IServiceCollection services,
        BlobServiceClient serviceClient,
        ServiceLifetime lifetime = ServiceLifetime.Scoped,
        DocumentStoreOptions documentStoreOptions = null)
        where T : class, new()
    {
        EnsureArg.IsNotNull(services, nameof(services));

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton<IDocumentStoreClient<T>>(sp => new DocumentStoreClient<T>(
                    new AzureBlobDocumentStoreProvider(sp.GetRequiredService<ILoggerFactory>(),
                        serviceClient ?? sp.GetRequiredService<BlobServiceClient>(),
                        options: documentStoreOptions)));

                break;
            case ServiceLifetime.Transient:
                services.AddTransient<IDocumentStoreClient<T>>(sp => new DocumentStoreClient<T>(
                    new AzureBlobDocumentStoreProvider(sp.GetRequiredService<ILoggerFactory>(),
                        serviceClient ?? sp.GetRequiredService<BlobServiceClient>(),
                        options: documentStoreOptions)));

                break;
            default:
                services.AddScoped<IDocumentStoreClient<T>>(sp => new DocumentStoreClient<T>(
                    new AzureBlobDocumentStoreProvider(sp.GetRequiredService<ILoggerFactory>(),
                        serviceClient ?? sp.GetRequiredService<BlobServiceClient>(),
                        options: documentStoreOptions)));

                break;
        }

        return new DocumentStoreBuilderContext<T>(services, lifetime);
    }

    /// <summary>
    /// Registers an Azure Table Storage backed document-store client for <typeparamref name="T" />.
    /// </summary>
    /// <typeparam name="T">The document payload type.</typeparam>
    /// <param name="services">The service collection to update.</param>
    /// <param name="provider">An optional pre-built Azure Table provider.</param>
    /// <param name="lifetime">The client lifetime to register.</param>
    /// <param name="documentStoreOptions">The optional document-store query safety options.</param>
    /// <returns>The document-store builder for adding behaviors.</returns>
    /// <example>
    /// <code>
    /// services.AddAzureTableDocumentStoreClient&lt;Person&gt;(
    ///     lifetime: ServiceLifetime.Scoped,
    ///     documentStoreOptions: new DocumentStoreOptions { MaxTake = 500 });
    /// </code>
    /// </example>
    public static DocumentStoreBuilderContext<T> AddAzureTableDocumentStoreClient<T>(
        this IServiceCollection services,
        AzureTableDocumentStoreProvider provider = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped,
        DocumentStoreOptions documentStoreOptions = null)
        where T : class, new()
    {
        EnsureArg.IsNotNull(services, nameof(services));

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton<IDocumentStoreClient<T>>(sp => new DocumentStoreClient<T>(provider ??
                    new AzureTableDocumentStoreProvider(sp.GetRequiredService<ILoggerFactory>(),
                        sp.GetRequiredService<TableServiceClient>(),
                        options: documentStoreOptions)));

                break;
            case ServiceLifetime.Transient:
                services.AddTransient<IDocumentStoreClient<T>>(sp => new DocumentStoreClient<T>(provider ??
                    new AzureTableDocumentStoreProvider(sp.GetRequiredService<ILoggerFactory>(),
                        sp.GetRequiredService<TableServiceClient>(),
                        options: documentStoreOptions)));

                break;
            default:
                services.AddScoped<IDocumentStoreClient<T>>(sp => new DocumentStoreClient<T>(provider ??
                    new AzureTableDocumentStoreProvider(sp.GetRequiredService<ILoggerFactory>(),
                        sp.GetRequiredService<TableServiceClient>(),
                        options: documentStoreOptions)));

                break;
        }

        return new DocumentStoreBuilderContext<T>(services, lifetime);
    }

    /// <summary>
    /// Registers an Azure Table Storage backed document-store client for <typeparamref name="T" /> using a table service client.
    /// </summary>
    /// <typeparam name="T">The document payload type.</typeparam>
    /// <param name="services">The service collection to update.</param>
    /// <param name="serviceClient">The optional table service client. When null, the client is resolved from services.</param>
    /// <param name="lifetime">The client lifetime to register.</param>
    /// <param name="documentStoreOptions">The optional document-store query safety options.</param>
    /// <returns>The document-store builder for adding behaviors.</returns>
    /// <example>
    /// <code>
    /// services.AddAzureTableDocumentStoreClient&lt;Person&gt;(
    ///     tableServiceClient,
    ///     documentStoreOptions: new DocumentStoreOptions { DefaultTake = 100 });
    /// </code>
    /// </example>
    public static DocumentStoreBuilderContext<T> AddAzureTableDocumentStoreClient<T>(
        this IServiceCollection services,
        TableServiceClient serviceClient,
        ServiceLifetime lifetime = ServiceLifetime.Scoped,
        DocumentStoreOptions documentStoreOptions = null)
        where T : class, new()
    {
        EnsureArg.IsNotNull(services, nameof(services));

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton<IDocumentStoreClient<T>>(sp => new DocumentStoreClient<T>(
                    new AzureTableDocumentStoreProvider(sp.GetRequiredService<ILoggerFactory>(),
                        serviceClient ?? sp.GetRequiredService<TableServiceClient>(),
                        options: documentStoreOptions)));

                break;
            case ServiceLifetime.Transient:
                services.AddTransient<IDocumentStoreClient<T>>(sp => new DocumentStoreClient<T>(
                    new AzureTableDocumentStoreProvider(sp.GetRequiredService<ILoggerFactory>(),
                        serviceClient ?? sp.GetRequiredService<TableServiceClient>(),
                        options: documentStoreOptions)));

                break;
            default:
                services.AddScoped<IDocumentStoreClient<T>>(sp => new DocumentStoreClient<T>(
                    new AzureTableDocumentStoreProvider(sp.GetRequiredService<ILoggerFactory>(),
                        serviceClient ?? sp.GetRequiredService<TableServiceClient>(),
                        options: documentStoreOptions)));

                break;
        }

        return new DocumentStoreBuilderContext<T>(services, lifetime);
    }
}
