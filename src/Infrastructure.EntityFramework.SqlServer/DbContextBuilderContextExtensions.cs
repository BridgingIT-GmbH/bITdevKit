// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using Diagnostics.HealthChecks;
using EntityFrameworkCore;

public static class DbContextBuilderContextExtensions
{
    public static SqlServerDbContextBuilderContext<TContext> WithHealthChecks<TContext>(
        this SqlServerDbContextBuilderContext<TContext> context,
        string healthQuery = default,
        string name = default,
        HealthStatus? failureStatus = default,
        IEnumerable<string> tags = null,
        TimeSpan? timeout = default)
        where TContext : DbContext
    {
        context.Services.AddHealthChecks()
            .AddSqlServer(context.ConnectionString,
                healthQuery ?? "SELECT 1;",
                null,
                name ?? $"{context.Provider.ToString().ToLower()} ({typeof(TContext).Name})",
                failureStatus,
                tags ?? ["ready"],
                timeout);

        return context;
    }
}