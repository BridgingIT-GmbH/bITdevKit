// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using Diagnostics.HealthChecks;
using EntityFrameworkCore;

public static class DbContextBuilderContextExtensions
{
    public static SqliteDbContextBuilderContext<TContext> WithHealthChecks<TContext>(
        this SqliteDbContextBuilderContext<TContext> context,
        string healthQuery = default,
        string name = default,
        HealthStatus? failureStatus = default,
        IEnumerable<string> tags = null,
        TimeSpan? timeout = default)
        where TContext : DbContext
    {
        context.Services.AddHealthChecks()
            .AddSqlite(context.ConnectionString,
                healthQuery ?? "select name from sqlite_master",
                null,
                name ?? $"{context.Provider.ToString().ToLower()} ({typeof(TContext).Name})",
                failureStatus,
                tags ?? new[] { "ready" },
                timeout);

        return context;
    }
}