// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using Configuration;
using EntityFrameworkCore;

/// <summary>
/// Represents postgres db context builder context.
/// </summary>
/// <typeparam name="TContext">The context type.</typeparam>
/// <param name="services">The service collection to configure.</param>
/// <param name="lifetime">The lifetime used by the operation.</param>
/// <param name="configuration">The configuration to apply.</param>
/// <param name="connectionString">The connection string used by the operation.</param>
/// <param name="provider">The provider used by the operation.</param>
public class PostgresDbContextBuilderContext<TContext>(
    IServiceCollection services,
    ServiceLifetime lifetime = ServiceLifetime.Scoped,
    IConfiguration configuration = null,
    string connectionString = null,
    Provider provider = Provider.SqlServer)
    : DbContextBuilderContext<TContext>(services, lifetime, configuration, connectionString, provider)
    where TContext : DbContext
{ }
