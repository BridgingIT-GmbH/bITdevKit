// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

public class SqlServerDbContextBuilderContext<TContext> : DbContextBuilderContext<TContext>
    where TContext : DbContext
{
    public SqlServerDbContextBuilderContext(
        IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped,
        IConfiguration configuration = null,
        string connectionString = null,
        Provider provider = Provider.SqlServer)
        : base(services, lifetime, configuration, connectionString, provider)
    {
    }
}