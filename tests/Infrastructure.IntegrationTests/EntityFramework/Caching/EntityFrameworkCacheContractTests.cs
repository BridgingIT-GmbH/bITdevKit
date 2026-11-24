// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Tests.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;
using BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Auditing.Fixtures;
using BridgingIT.DevKit.Tests.Caching;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Runs the ordinary shared application-cache contract against a host-migrated same-host SQLite store.</summary>
public sealed class EntityFrameworkCacheContractTests : EntityFrameworkCacheContractTestsBase
{
    /// <inheritdoc />
    protected override async Task<CacheProviderContractFixture> CreateAsync(Action<ApplicationCachingBuilderContext> configure = null)
    {
        var directory = Path.Combine(Path.GetTempPath(), "bdk-cache-contract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var connectionString = new SqliteConnectionStringBuilder { DataSource = Path.Combine(directory, "cache.db"), Pooling = false }.ToString();
        return await CreateStoreAsync("Sqlite", () => new SqliteConnection(connectionString), configure, () =>
        {
            Directory.Delete(directory, true);
            return ValueTask.CompletedTask;
        });
    }
}

/// <summary>Runs the identical portable application-cache vectors against a consumer-migrated SQL Server database.</summary>
/// <param name="fixture">The existing disposable relational test-container fixture.</param>
[Collection(nameof(AuditRelationalCollection))]
public sealed class SqlServerEntityFrameworkCacheContractTests(AuditRelationalFixture fixture) : EntityFrameworkCacheContractTestsBase
{
    /// <inheritdoc />
    protected override async Task<CacheProviderContractFixture> CreateAsync(Action<ApplicationCachingBuilderContext> configure = null) =>
        await CreateStoreAsync("SqlServer", await fixture.CreateDatabaseAsync("SqlServer"), configure);
}

/// <summary>Runs the identical portable application-cache vectors against a consumer-migrated PostgreSQL database.</summary>
/// <param name="fixture">The existing disposable relational test-container fixture.</param>
[Collection(nameof(AuditRelationalCollection))]
public sealed class PostgresEntityFrameworkCacheContractTests(AuditRelationalFixture fixture) : EntityFrameworkCacheContractTestsBase
{
    /// <inheritdoc />
    protected override async Task<CacheProviderContractFixture> CreateAsync(Action<ApplicationCachingBuilderContext> configure = null) =>
        await CreateStoreAsync("Postgres", await fixture.CreateDatabaseAsync("Postgres"), configure);
}

/// <summary>Keeps the complete EF provider composition identical while selecting only the host database engine and connections.</summary>
public abstract class EntityFrameworkCacheContractTestsBase : CacheProviderContractTestsBase
{
    /// <summary>Deploys the consumer-owned migrations and composes the ordinary application cache with its real relational provider.</summary>
    /// <param name="engine">The registered native Sqlite, SqlServer, or Postgres engine.</param>
    /// <param name="connections">Creates an independently owned connection for every provider operation.</param>
    /// <param name="configure">The unchanged common contract scenario's optional policy configuration.</param>
    /// <param name="cleanup">Optional scratch cleanup after every service and provider handle has been disposed.</param>
    /// <returns>The same portable contract fixture, backed by the selected real database engine.</returns>
    protected static async Task<CacheProviderContractFixture> CreateStoreAsync(string engine, Func<DbConnection> connections,
        Action<ApplicationCachingBuilderContext> configure = null, Func<ValueTask> cleanup = null)
    {
        CacheTestHostDbContext Context()
        {
            var builder = new DbContextOptionsBuilder<CacheTestHostDbContext>();
            var connection = connections();
            switch (engine)
            {
                case "SqlServer": builder.UseSqlServer(connection, true, migration => migration.MigrationsAssembly(typeof(CacheTestHostDbContext).Assembly.GetName().Name)); break;
                case "Postgres": builder.UseNpgsql(connection, true, migration => migration.MigrationsAssembly(typeof(CacheTestHostDbContext).Assembly.GetName().Name)); break;
                case "Sqlite": builder.UseSqlite(connection, true, migration => migration.MigrationsAssembly(typeof(CacheTestHostDbContext).Assembly.GetName().Name)); break;
                default: throw new ArgumentOutOfRangeException(nameof(engine));
            }

            var context = new CacheTestHostDbContext(builder.Options);
            if (engine == "Sqlite")
            {
                context.Database.OpenConnection();
                context.Database.ExecuteSqlRaw("PRAGMA busy_timeout=5000;");
            }

            return context;
        }

        await using (var bootstrap = Context())
        {
            await bootstrap.Database.MigrateAsync();
            if (engine == "Sqlite") { await bootstrap.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;"); }
        }

        var clock = new CacheContractClock();
        var options = new CacheProviderOptions { Kind = "EntityFramework", Engine = engine };
        var services = CacheProviderContractFixture.CreateServices(clock, "EntityFramework", EntityFrameworkCacheStoreProvider.CapabilitiesFor(options),
            (providerServices, name, configured) => new EntityFrameworkCacheStoreProvider(name, configured with { Engine = engine }, Context,
                clock, providerServices.GetRequiredService<CacheRuntimeResources>()), configure);
        return new(services.BuildServiceProvider(), clock, cleanup);
    }
}