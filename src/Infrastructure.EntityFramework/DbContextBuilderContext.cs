// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using Configuration;

/// <summary>
/// Represents db context builder context.
/// </summary>
/// <typeparam name="TContext">The context type.</typeparam>
/// <param name="services">The service collection to configure.</param>
/// <param name="lifetime">The lifetime used by the operation.</param>
/// <param name="configuration">The configuration to apply.</param>
/// <param name="connectionString">The connection string used by the operation.</param>
/// <param name="provider">The provider used by the operation.</param>
public class DbContextBuilderContext<TContext>(
    IServiceCollection services,
    ServiceLifetime lifetime = ServiceLifetime.Scoped,
    IConfiguration configuration = null,
    string connectionString = null,
    Provider provider = Provider.SqlServer)
    where TContext : DbContext
{
    /// <summary>
    /// Gets the services.
    /// </summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>
    /// Gets the lifetime.
    /// </summary>
    public ServiceLifetime Lifetime { get; } = lifetime;

    /// <summary>
    /// Gets the configuration.
    /// </summary>
    public IConfiguration Configuration { get; } = configuration;

    /// <summary>
    /// Gets the connection string.
    /// </summary>
    public string ConnectionString { get; } = connectionString;

    /// <summary>
    /// Gets the provider.
    /// </summary>
    public Provider Provider { get; } = provider;
}

/// <summary>
/// Defines the supported provider values.
/// </summary>
public enum Provider
{
    /// <summary>
    /// Represents the sql server value.
    /// </summary>
    SqlServer,
    /// <summary>
    /// Represents the sqlite value.
    /// </summary>
    Sqlite,
    /// <summary>
    /// Represents the in memory value.
    /// </summary>
    InMemory,
    /// <summary>
    /// Represents the cosmos value.
    /// </summary>
    Cosmos,
    /// <summary>
    /// Represents the postgres value.
    /// </summary>
    Postgres
}
