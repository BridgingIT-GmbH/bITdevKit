// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
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