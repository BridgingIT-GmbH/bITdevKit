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
    /// Registers an Azure Blob Storage backed document-store client within a top-level document-storage registration flow.
    /// </summary>
    /// <typeparam name="T">The document payload type.</typeparam>
    /// <param name="context">The document-storage builder context.</param>
    /// <param name="provider">An optional pre-built Azure Blob provider.</param>
    /// <param name="lifetime">The optional client lifetime override.</param>
    /// <param name="documentStoreOptions">The optional document-store query safety options.</param>
    /// <returns>The current document-storage builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddDocumentStorage()
    ///     .WithBehavior&lt;LoggingDocumentStoreClientBehavior&lt;Person&gt;&gt;()
    ///     .WithAzureBlobClient&lt;Person&gt;();
    /// </code>
    /// </example>
    public static DocumentStorageBuilderContext WithAzureBlobClient<T>(
        this DocumentStorageBuilderContext context,
        AzureBlobDocumentStoreProvider provider = null,
        ServiceLifetime? lifetime = null,
        DocumentStoreOptions documentStoreOptions = null)
        where T : class, new()
    {
        EnsureArg.IsNotNull(context, nameof(context));

        if (!context.Options.IsEnabled)
        {
            return context;
        }

        context.Services.AddAzureBlobDocumentStoreClient<T>(
            provider,
            lifetime ?? context.Lifetime,
            documentStoreOptions);

        return context.RegisterClient<T>(
            "Azure Blob Storage",
            capabilities: provider?.Capabilities ?? CreateAzureBlobCapabilities());
    }

    /// <summary>
    /// Registers an Azure Blob Storage backed document-store client within a top-level document-storage registration flow.
    /// </summary>
    /// <typeparam name="T">The document payload type.</typeparam>
    /// <param name="context">The document-storage builder context.</param>
    /// <param name="serviceClient">The optional blob service client. When null, the client is resolved from services.</param>
    /// <param name="lifetime">The optional client lifetime override.</param>
    /// <param name="documentStoreOptions">The optional document-store query safety options.</param>
    /// <returns>The current document-storage builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddDocumentStorage()
    ///     .WithAzureBlobClient&lt;Person&gt;(blobServiceClient);
    /// </code>
    /// </example>
    public static DocumentStorageBuilderContext WithAzureBlobClient<T>(
        this DocumentStorageBuilderContext context,
        BlobServiceClient serviceClient,
        ServiceLifetime? lifetime = null,
        DocumentStoreOptions documentStoreOptions = null)
        where T : class, new()
    {
        EnsureArg.IsNotNull(context, nameof(context));

        if (!context.Options.IsEnabled)
        {
            return context;
        }

        context.Services.AddAzureBlobDocumentStoreClient<T>(
            serviceClient,
            lifetime ?? context.Lifetime,
            documentStoreOptions);

        return context.RegisterClient<T>(
            "Azure Blob Storage",
            capabilities: CreateAzureBlobCapabilities());
    }

    /// <summary>
    /// Registers an Azure Table Storage backed document-store client within a top-level document-storage registration flow.
    /// </summary>
    /// <typeparam name="T">The document payload type.</typeparam>
    /// <param name="context">The document-storage builder context.</param>
    /// <param name="provider">An optional pre-built Azure Table provider.</param>
    /// <param name="lifetime">The optional client lifetime override.</param>
    /// <param name="documentStoreOptions">The optional document-store query safety options.</param>
    /// <returns>The current document-storage builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddDocumentStorage()
    ///     .WithAzureTableClient&lt;Person&gt;();
    /// </code>
    /// </example>
    public static DocumentStorageBuilderContext WithAzureTableClient<T>(
        this DocumentStorageBuilderContext context,
        AzureTableDocumentStoreProvider provider = null,
        ServiceLifetime? lifetime = null,
        DocumentStoreOptions documentStoreOptions = null)
        where T : class, new()
    {
        EnsureArg.IsNotNull(context, nameof(context));

        if (!context.Options.IsEnabled)
        {
            return context;
        }

        context.Services.AddAzureTableDocumentStoreClient<T>(
            provider,
            lifetime ?? context.Lifetime,
            documentStoreOptions);

        return context.RegisterClient<T>(
            "Azure Table Storage",
            capabilities: provider?.Capabilities ?? CreateAzureTableCapabilities());
    }

    /// <summary>
    /// Registers an Azure Table Storage backed document-store client within a top-level document-storage registration flow.
    /// </summary>
    /// <typeparam name="T">The document payload type.</typeparam>
    /// <param name="context">The document-storage builder context.</param>
    /// <param name="serviceClient">The optional table service client. When null, the client is resolved from services.</param>
    /// <param name="lifetime">The optional client lifetime override.</param>
    /// <param name="documentStoreOptions">The optional document-store query safety options.</param>
    /// <returns>The current document-storage builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddDocumentStorage()
    ///     .WithAzureTableClient&lt;Person&gt;(tableServiceClient);
    /// </code>
    /// </example>
    public static DocumentStorageBuilderContext WithAzureTableClient<T>(
        this DocumentStorageBuilderContext context,
        TableServiceClient serviceClient,
        ServiceLifetime? lifetime = null,
        DocumentStoreOptions documentStoreOptions = null)
        where T : class, new()
    {
        EnsureArg.IsNotNull(context, nameof(context));

        if (!context.Options.IsEnabled)
        {
            return context;
        }

        context.Services.AddAzureTableDocumentStoreClient<T>(
            serviceClient,
            lifetime ?? context.Lifetime,
            documentStoreOptions);

        return context.RegisterClient<T>(
            "Azure Table Storage",
            capabilities: CreateAzureTableCapabilities());
    }

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

    private static DocumentStoreProviderCapabilities CreateAzureBlobCapabilities() => new()
    {
        FullMatch = DocumentQuerySupport.SupportedEfficiently,
        RowKeyPrefixMatch = DocumentQuerySupport.SupportedEfficiently,
        RowKeySuffixMatch = DocumentQuerySupport.SupportedClientSide,
        FullScan = DocumentQuerySupport.SupportedServerSide,
        KeyListing = DocumentQuerySupport.SupportedEfficiently,
        SupportsContinuationPaging = true,
        SupportsServerSideCount = false,
        SupportsKeyOnlyProjection = true
    };

    private static DocumentStoreProviderCapabilities CreateAzureTableCapabilities() => new()
    {
        FullMatch = DocumentQuerySupport.SupportedEfficiently,
        RowKeyPrefixMatch = DocumentQuerySupport.SupportedEfficiently,
        RowKeySuffixMatch = DocumentQuerySupport.Unsupported,
        FullScan = DocumentQuerySupport.SupportedServerSide,
        KeyListing = DocumentQuerySupport.SupportedEfficiently,
        SupportsContinuationPaging = true,
        SupportsServerSideCount = false,
        SupportsKeyOnlyProjection = true
    };
}
