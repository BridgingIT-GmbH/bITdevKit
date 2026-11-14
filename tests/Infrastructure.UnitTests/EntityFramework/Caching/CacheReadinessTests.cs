// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;
using BridgingIT.DevKit.Infrastructure.Tests.Caching;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

public class CacheReadinessTests
{
    [Theory]
    [InlineData("Sqlite")]
    [InlineData("SqlServer")]
    [InlineData("Postgres")]
    public void EveryEngine_HasACompleteMigrationModelAndTruthfulCapabilities(string engine)
    {
        var builder = new DbContextOptionsBuilder<CacheTestHostDbContext>();
        switch (engine)
        {
            case "Sqlite": builder.UseSqlite("Data Source=:memory:", migration => migration.MigrationsAssembly(typeof(CacheTestHostDbContext).Assembly.GetName().Name)); break;
            case "SqlServer": builder.UseSqlServer("Server=localhost;Database=CacheModel;Integrated Security=true", migration => migration.MigrationsAssembly(typeof(CacheTestHostDbContext).Assembly.GetName().Name)); break;
            case "Postgres": builder.UseNpgsql("Host=localhost;Database=cache_model;Username=model;Password=model", migration => migration.MigrationsAssembly(typeof(CacheTestHostDbContext).Assembly.GetName().Name)); break;
        }

        using var context = new CacheTestHostDbContext(builder.Options);
        context.Database.HasPendingModelChanges().ShouldBeFalse();
        var capabilities = EntityFrameworkCacheStoreProvider.CapabilitiesFor(new() { Engine = engine });
        capabilities.SharedStoreCoordination.ShouldBeTrue();
        capabilities.NativeAsyncIO.ShouldBe(engine != "Sqlite");
        capabilities.LocalHostOnly.ShouldBe(engine == "Sqlite");
        capabilities.SeekableRead.ShouldBeFalse();
    }

    [Fact]
    public async Task MissingSchema_IsUnavailableAndNoTablesAreCreatedByReadiness()
    {
        var path = Path.Combine(Path.GetTempPath(), "cache-readiness-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            var options = Options(path);
            await using (var bootstrap = new CacheTestHostDbContext(options))
            {
                await bootstrap.Database.OpenConnectionAsync();
                await bootstrap.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
            }

            await using var sut = new EntityFrameworkCacheStoreProvider("database", new(), () => Create(options));
            var result = await sut.InitializeAsync([Definition()]);
            result.IsFailure.ShouldBeTrue();
            result.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.ProviderUnavailable);
            await using var connection = new SqliteConnection("Data Source=" + path + ";Pooling=False");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table'";
            Convert.ToInt32(await command.ExecuteScalarAsync()).ShouldBe(0);
        }
        finally { Delete(path); }
    }

    [Fact]
    public async Task DeployedSchema_BindsContractsAndRejectsIncompatibleRetainedSchema()
    {
        var path = Path.Combine(Path.GetTempPath(), "cache-readiness-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            var options = Options(path);
            await using (var bootstrap = new CacheTestHostDbContext(options))
            {
                await bootstrap.Database.MigrateAsync();
                await bootstrap.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
            }

            await using var sut = new EntityFrameworkCacheStoreProvider("database", new(), () => Create(options));
            var initialized = await sut.InitializeAsync([Definition()]);
            initialized.IsSuccess.ShouldBeTrue(string.Join(", ", initialized.Errors.Select(e => e.Message)));
            (await sut.InitializeAsync([Definition()])).IsSuccess.ShouldBeTrue();
            await using var incompatible = new EntityFrameworkCacheStoreProvider("database", new(), () => Create(options));

            var rejected = await incompatible.InitializeAsync([Definition() with { ContractId = "other-format" }]);

            rejected.IsFailure.ShouldBeTrue();
            rejected.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Compatibility);
            await using var verify = new CacheTestHostDbContext(options);
            (await verify.Set<CacheContractEntity>().SingleAsync()).ContractAlias.ShouldBe("TEXT");
            (await verify.Set<CacheStoreEntity>().SingleAsync()).MetadataBytes.ShouldBe(65536);
        }
        finally { Delete(path); }
    }

    private static CacheProviderCollection Definition() => new("application", "items", "v1", "text", [], new());

    private static DbContextOptions<CacheTestHostDbContext> Options(string path) => new DbContextOptionsBuilder<CacheTestHostDbContext>()
        .UseSqlite("Data Source=" + path + ";Pooling=False;Default Timeout=5", migration => migration.MigrationsAssembly(typeof(CacheTestHostDbContext).Assembly.GetName().Name)).Options;

    private static CacheTestHostDbContext Create(DbContextOptions<CacheTestHostDbContext> options)
    {
        var context = new CacheTestHostDbContext(options);
        context.Database.OpenConnection();
        context.Database.ExecuteSqlRaw("PRAGMA busy_timeout=5000;");
        return context;
    }

    private static void Delete(string path)
    {
        File.Delete(path);
        File.Delete(path + "-wal");
        File.Delete(path + "-shm");
    }
}