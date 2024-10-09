// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;
using Configuration;

public class EntityFrameworkRepositoryBuilderContext<TEntity, TContext>(
    IServiceCollection services,
    ServiceLifetime lifetime = ServiceLifetime.Scoped,
    IConfiguration configuration = null) : RepositoryBuilderContext<TEntity>(services, lifetime, configuration)
    where TEntity : class, IEntity
    where TContext : DbContext
{
    public EntityFrameworkRepositoryBuilderContext<TEntity, TContext> WithTransactions()
    {
        switch (this.Lifetime)
        {
            case ServiceLifetime.Singleton:
                this.Services
                    .AddSingleton<IRepositoryTransaction<TEntity>,
                        EntityFrameworkTransactionWrapper<TEntity, TContext>>();

                break;
            case ServiceLifetime.Transient:
                this.Services
                    .AddTransient<IRepositoryTransaction<TEntity>,
                        EntityFrameworkTransactionWrapper<TEntity, TContext>>();

                break;
            default:
                this.Services
                    .AddScoped<IRepositoryTransaction<TEntity>, EntityFrameworkTransactionWrapper<TEntity, TContext>>();

                break;
        }

        return this;
    }
}