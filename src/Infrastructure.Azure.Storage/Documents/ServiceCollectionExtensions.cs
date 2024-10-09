// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    public static DocumentStoreBuilderContext<T> AddAzureBlobDocumentStoreClient<T>(
        this IServiceCollection services,
        AzureBlobDocumentStoreProvider provider = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where T : class, new()
    {
        EnsureArg.IsNotNull(services, nameof(services));

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton<IDocumentStoreClient<T>>(sp => new DocumentStoreClient<T>(provider ??
                    new AzureBlobDocumentStoreProvider(sp.GetRequiredService<ILoggerFactory>(),
                        sp.GetRequiredService<BlobServiceClient>())));

                break;
            case ServiceLifetime.Transient:
                services.AddTransient<IDocumentStoreClient<T>>(sp => new DocumentStoreClient<T>(provider ??
                    new AzureBlobDocumentStoreProvider(sp.GetRequiredService<ILoggerFactory>(),
                        sp.GetRequiredService<BlobServiceClient>())));

                break;
            default:
                services.AddScoped<IDocumentStoreClient<T>>(sp => new DocumentStoreClient<T>(provider ??
                    new AzureBlobDocumentStoreProvider(sp.GetRequiredService<ILoggerFactory>(),
                        sp.GetRequiredService<BlobServiceClient>())));

                break;
        }

        return new DocumentStoreBuilderContext<T>(services, lifetime);
    }

    public static DocumentStoreBuilderContext<T> AddAzureBlobDocumentStoreClient<T>(
        this IServiceCollection services,
        BlobServiceClient serviceClient,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where T : class, new()
    {
        EnsureArg.IsNotNull(services, nameof(services));

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton<IDocumentStoreClient<T>>(sp => new DocumentStoreClient<T>(
                    new AzureBlobDocumentStoreProvider(sp.GetRequiredService<ILoggerFactory>(),
                        serviceClient ?? sp.GetRequiredService<BlobServiceClient>())));

                break;
            case ServiceLifetime.Transient:
                services.AddTransient<IDocumentStoreClient<T>>(sp => new DocumentStoreClient<T>(
                    new AzureBlobDocumentStoreProvider(sp.GetRequiredService<ILoggerFactory>(),
                        serviceClient ?? sp.GetRequiredService<BlobServiceClient>())));

                break;
            default:
                services.AddScoped<IDocumentStoreClient<T>>(sp => new DocumentStoreClient<T>(
                    new AzureBlobDocumentStoreProvider(sp.GetRequiredService<ILoggerFactory>(),
                        serviceClient ?? sp.GetRequiredService<BlobServiceClient>())));

                break;
        }

        return new DocumentStoreBuilderContext<T>(services, lifetime);
    }

    public static DocumentStoreBuilderContext<T> AddAzureTableDocumentStoreClient<T>(
        this IServiceCollection services,
        AzureTableDocumentStoreProvider provider = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where T : class, new()
    {
        EnsureArg.IsNotNull(services, nameof(services));

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton<IDocumentStoreClient<T>>(sp => new DocumentStoreClient<T>(provider ??
                    new AzureTableDocumentStoreProvider(sp.GetRequiredService<ILoggerFactory>(),
                        sp.GetRequiredService<TableServiceClient>())));

                break;
            case ServiceLifetime.Transient:
                services.AddTransient<IDocumentStoreClient<T>>(sp => new DocumentStoreClient<T>(provider ??
                    new AzureTableDocumentStoreProvider(sp.GetRequiredService<ILoggerFactory>(),
                        sp.GetRequiredService<TableServiceClient>())));

                break;
            default:
                services.AddScoped<IDocumentStoreClient<T>>(sp => new DocumentStoreClient<T>(provider ??
                    new AzureTableDocumentStoreProvider(sp.GetRequiredService<ILoggerFactory>(),
                        sp.GetRequiredService<TableServiceClient>())));

                break;
        }

        return new DocumentStoreBuilderContext<T>(services, lifetime);
    }

    public static DocumentStoreBuilderContext<T> AddAzureTableDocumentStoreClient<T>(
        this IServiceCollection services,
        TableServiceClient serviceClient,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where T : class, new()
    {
        EnsureArg.IsNotNull(services, nameof(services));

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton<IDocumentStoreClient<T>>(sp => new DocumentStoreClient<T>(
                    new AzureTableDocumentStoreProvider(sp.GetRequiredService<ILoggerFactory>(),
                        serviceClient ?? sp.GetRequiredService<TableServiceClient>())));

                break;
            case ServiceLifetime.Transient:
                services.AddTransient<IDocumentStoreClient<T>>(sp => new DocumentStoreClient<T>(
                    new AzureTableDocumentStoreProvider(sp.GetRequiredService<ILoggerFactory>(),
                        serviceClient ?? sp.GetRequiredService<TableServiceClient>())));

                break;
            default:
                services.AddScoped<IDocumentStoreClient<T>>(sp => new DocumentStoreClient<T>(
                    new AzureTableDocumentStoreProvider(sp.GetRequiredService<ILoggerFactory>(),
                        serviceClient ?? sp.GetRequiredService<TableServiceClient>())));

                break;
        }

        return new DocumentStoreBuilderContext<T>(services, lifetime);
    }
}