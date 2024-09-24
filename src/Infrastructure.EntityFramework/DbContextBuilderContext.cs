// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using Configuration;
using EntityFrameworkCore;

public class DbContextBuilderContext<TContext>(
    IServiceCollection services,
    ServiceLifetime lifetime = ServiceLifetime.Scoped,
    IConfiguration configuration = null,
    string connectionString = null,
    Provider provider = Provider.SqlServer)
    where TContext : DbContext
{
    public IServiceCollection Services { get; } = services;

    public ServiceLifetime Lifetime { get; } = lifetime;

    public IConfiguration Configuration { get; } = configuration;

    public string ConnectionString { get; } = connectionString;

    public Provider Provider { get; } = provider;
}

public enum Provider
{
    SqlServer,
    Sqlite,
    InMemory,
    Cosmos
}