// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using Configuration;
using EntityFrameworkCore;

/// <summary>
/// Represents sql server db context builder context.
/// </summary>
/// <typeparam name="TContext">The context type.</typeparam>
/// <param name="services">The service collection to configure.</param>
/// <param name="lifetime">The lifetime used by the operation.</param>
/// <param name="configuration">The configuration to apply.</param>
/// <param name="connectionString">The connection string used by the operation.</param>
/// <param name="provider">The provider used by the operation.</param>
/// <example><code>services.AddSqlServerDbContext&lt;CatalogDbContext&gt;(connectionString).WithDbContextFactory();</code></example>
public class SqlServerDbContextBuilderContext<TContext>(
    IServiceCollection services,
    ServiceLifetime lifetime = ServiceLifetime.Scoped,
    IConfiguration configuration = null,
    string connectionString = null,
    Provider provider = Provider.SqlServer)
    : DbContextBuilderContext<TContext>(services, lifetime, configuration, connectionString, provider)
    where TContext : DbContext
{
    /// <summary>Gets the lazy native-options callback shared with an optional independently owned context factory.</summary>
    /// <remarks>The callback excludes registered request-scoped interceptors and performs no database I/O. It is absent for manually constructed builders that supply no native configuration.</remarks>
    public Action<IServiceProvider, DbContextOptionsBuilder> NativeFactoryOptions { get; init; }
}