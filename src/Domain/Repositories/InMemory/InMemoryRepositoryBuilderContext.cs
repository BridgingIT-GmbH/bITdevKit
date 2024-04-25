// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using Microsoft.Extensions.Configuration;

public class InMemoryRepositoryBuilderContext<TEntity, TContext> : RepositoryBuilderContext<TEntity>
    where TEntity : class, IEntity
    where TContext : InMemoryContext<TEntity>
{
    public InMemoryRepositoryBuilderContext(IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Scoped, IConfiguration configuration = null)
        : base(services, lifetime, configuration)
    {
    }
}