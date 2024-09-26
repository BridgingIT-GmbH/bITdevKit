// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;
using EntityFrameworkCore;

public static partial class ServiceCollectionExtensions
{
    public static DocumentStoreBuilderContext<T> AddEntityFrameworkDocumentStoreClient<T, TContext>(
        this IServiceCollection services,
        EntityFrameworkDocumentStoreProvider<TContext> provider = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where T : class, new()
        where TContext : DbContext, IDocumentStoreContext
    {
        EnsureArg.IsNotNull(services, nameof(services));

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton<IDocumentStoreClient<T>>(sp => new DocumentStoreClient<T>(
                    provider ?? new EntityFrameworkDocumentStoreProvider<TContext>(sp.GetRequiredService<TContext>())));

                break;
            case ServiceLifetime.Transient:
                services.AddTransient<IDocumentStoreClient<T>>(sp => new DocumentStoreClient<T>(
                    provider ?? new EntityFrameworkDocumentStoreProvider<TContext>(sp.GetRequiredService<TContext>())));

                break;
            default:
                services.AddScoped<IDocumentStoreClient<T>>(sp => new DocumentStoreClient<T>(
                    provider ?? new EntityFrameworkDocumentStoreProvider<TContext>(sp.GetRequiredService<TContext>())));

                break;
        }

        return new DocumentStoreBuilderContext<T>(services, lifetime);
    }
}