// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.Azure;
using global::Azure.Data.Tables;
using global::Azure.Storage.Blobs;

public static partial class ServiceCollectionExtensions
{
    public static AzureStorageBuilderContext AddAzureBlobServiceClient(
        this IServiceCollection services,
        BlobServiceClient serviceClient,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        return services.AddAzureBlobServiceClient(null, serviceClient, lifetime);
    }

    public static AzureStorageBuilderContext AddAzureBlobServiceClient(
        this IServiceCollection services,
        Builder<AzureBlobServiceOptionsBuilder, AzureBlobServiceOptions> optionsBuilder,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        return services.AddAzureBlobServiceClient(optionsBuilder(new AzureBlobServiceOptionsBuilder()).Build(),
            null,
            lifetime);
    }

    public static AzureStorageBuilderContext AddAzureBlobServiceClient(
        this IServiceCollection services,
        AzureBlobServiceOptions options,
        BlobServiceClient serviceClient = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        options ??= new AzureBlobServiceOptions();
        options.ClientOptions ??= new BlobClientOptions();

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton(sp =>
                    serviceClient ?? new BlobServiceClient(options.ConnectionString, options.ClientOptions));

                break;
            case ServiceLifetime.Transient:
                services.AddTransient(sp =>
                    serviceClient ?? new BlobServiceClient(options.ConnectionString, options.ClientOptions));

                break;
            default:
                services.AddScoped(sp =>
                    serviceClient ?? new BlobServiceClient(options.ConnectionString, options.ClientOptions));

                break;
        }

        return new AzureStorageBuilderContext(services, lifetime, null, options.ConnectionString);
    }

    public static AzureStorageBuilderContext AddAzureTableServiceClient(
        this IServiceCollection services,
        TableServiceClient serviceClient,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        return services.AddAzureTableServiceClient(null, serviceClient, lifetime);
    }

    public static AzureStorageBuilderContext AddAzureTableServiceClient(
        this IServiceCollection services,
        Builder<AzureTableServiceOptionsBuilder, AzureTableServiceOptions> optionsBuilder,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        return services.AddAzureTableServiceClient(optionsBuilder(new AzureTableServiceOptionsBuilder()).Build(),
            null,
            lifetime);
    }

    public static AzureStorageBuilderContext AddAzureTableServiceClient(
        this IServiceCollection services,
        AzureTableServiceOptions options,
        TableServiceClient serviceClient = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        options ??= new AzureTableServiceOptions();
        options.ClientOptions ??= new TableClientOptions();

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton(sp =>
                    serviceClient ?? new TableServiceClient(options.ConnectionString, options.ClientOptions));

                break;
            case ServiceLifetime.Transient:
                services.AddTransient(sp =>
                    serviceClient ?? new TableServiceClient(options.ConnectionString, options.ClientOptions));

                break;
            default:
                services.AddScoped(sp =>
                    serviceClient ?? new TableServiceClient(options.ConnectionString, options.ClientOptions));

                break;
        }

        return new AzureStorageBuilderContext(services, lifetime, null, options.ConnectionString, Service.Table);
    }
}