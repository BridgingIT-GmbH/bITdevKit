// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

public class DbContextBuilderContext<TContext>
    where TContext : DbContext
{
    public DbContextBuilderContext(
        IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped,
        IConfiguration configuration = null,
        string connectionString = null,
        Provider provider = Provider.SqlServer)
    {
        this.Services = services;
        this.Lifetime = lifetime;
        this.Configuration = configuration;
        this.ConnectionString = connectionString;
        this.Provider = provider;
    }

    public IServiceCollection Services { get; }

    public ServiceLifetime Lifetime { get; }

    public IConfiguration Configuration { get; }

    public string ConnectionString { get; }

    public Provider Provider { get; }
}

public enum Provider
{
    SqlServer,
    Sqlite,
    InMemory,
    Cosmos
}