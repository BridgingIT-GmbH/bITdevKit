// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using Azure.Cosmos;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.Azure;
using CosmosClientOptions = BridgingIT.DevKit.Infrastructure.Azure.CosmosClientOptions;

public static partial class ServiceCollectionExtensions
{
    public static CosmosClientBuilderContext AddCosmosClient(
        this IServiceCollection services,
        CosmosClient client,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        return services.AddCosmosClient(null, client, lifetime);
    }

    public static CosmosClientBuilderContext AddCosmosClient(
        this IServiceCollection services,
        Builder<CosmosClientOptionsBuilder, CosmosClientOptions> optionsBuilder,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        return services.AddCosmosClient(optionsBuilder(new CosmosClientOptionsBuilder()).Build(), null, lifetime);
    }

    public static CosmosClientBuilderContext AddCosmosClient(
        this IServiceCollection services,
        CosmosClientOptions options,
        CosmosClient client = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        options ??= new CosmosClientOptions();
        options.ClientOptions ??= new Azure.Cosmos.CosmosClientOptions
        {
            ConnectionMode = ConnectionMode.Direct,
            // TODO: systemtextjson still has issues deserializing types with no public or multiple constructors, that is an issue for ValueObjects.
            //Serializer = new CosmosSystemTextJsonSerializer(
            //    new()
            //    {
            //        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            //        WriteIndented = true,
            //        PropertyNameCaseInsensitive = true,
            //        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            //    })
            Serializer = new CosmosJsonNetSerializer(DefaultNewtonsoftSerializerSettings.Create())
            //SerializerOptions = new CosmosSerializationOptions
            //{
            //    Indented = true,
            //    IgnoreNullValues = false,
            //    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            //}
        };

        if (options.IgnoreServerCertificateValidation)
        {
            options.ClientOptions.HttpClientFactory ??= () =>
            {
                return new HttpClient(new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                });
            };
        }

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton(sp =>
                    client ?? new CosmosClient(options.ConnectionString, options.ClientOptions));

                break;
            case ServiceLifetime.Transient:
                services.AddTransient(sp =>
                    client ?? new CosmosClient(options.ConnectionString, options.ClientOptions));

                break;
            default:
                services.AddScoped(sp => client ?? new CosmosClient(options.ConnectionString, options.ClientOptions));

                break;
        }

        return new CosmosClientBuilderContext(services, lifetime, null, options.ConnectionString);
    }
}