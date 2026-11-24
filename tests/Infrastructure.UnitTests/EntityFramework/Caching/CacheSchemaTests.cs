// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Caching;

using System.Text;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using BridgingIT.DevKit.Infrastructure.Tests.Caching;

public class CacheSchemaTests
{
    [Fact]
    public void RuntimePackage_ContainsNoCacheMigrationsOrSnapshotArtifacts()
    {
        typeof(CacheDbContext).Assembly.GetTypes().Where(type =>
            type.Namespace?.Contains("Caching", StringComparison.Ordinal) == true &&
            (typeof(Migration).IsAssignableFrom(type) || typeof(ModelSnapshot).IsAssignableFrom(type))).ShouldBeEmpty();
    }

    [Fact]
    public void Model_UsesOnePayloadGenerationAndKeepsPublicationAndOperationCounters()
    {
        using var sut = new CacheDbContext(new DbContextOptionsBuilder<CacheDbContext>().UseSqlite("Data Source=:memory:", migration => migration.MigrationsAssembly(typeof(CacheTestHostDbContext).Assembly.GetName().Name)).Options);

        sut.Model.FindEntityType(typeof(CacheStoreEntity)).FindProperty("Revision").ShouldBeNull();
        sut.Model.FindEntityType(typeof(CacheKeyEntity)).FindProperty("Revision").ShouldBeNull();
        var entry = sut.Model.FindEntityType(typeof(CacheEntryEntity));
        entry.FindProperty("Version").ShouldBeNull();
        entry.FindProperty(nameof(CacheEntryEntity.PayloadId)).GetTypeMapping().Converter.ProviderClrType.ShouldBe(typeof(byte[]));
        sut.Model.FindEntityType(typeof(CacheCollectionEntity)).FindProperty(nameof(CacheCollectionEntity.Revision)).ShouldNotBeNull();
        sut.Model.FindEntityType(typeof(CacheOperationEntity)).FindProperty(nameof(CacheOperationEntity.Revision)).ShouldNotBeNull();
    }

    [Fact]
    public void ConsumerMigrationSnapshot_MatchesTheAnnotatedModel()
    {
        using var sut = new CacheTestHostDbContext(new DbContextOptionsBuilder<CacheTestHostDbContext>().UseSqlite("Data Source=:memory:", migration => migration.MigrationsAssembly(typeof(CacheTestHostDbContext).Assembly.GetName().Name)).Options);
        var snapshot = sut.GetService<IModelRuntimeInitializer>().Initialize(new CacheTestHostDbContextModelSnapshot().Model, designTime: true);
        var current = sut.GetService<IDesignTimeModel>().Model;
        var differences = sut.GetService<IMigrationsModelDiffer>().GetDifferences(snapshot.GetRelationalModel(), current.GetRelationalModel());

        differences.ShouldBeEmpty(string.Join('\n', differences.Select(d => d.GetType().Name + ": " + string.Join(", ", d.GetType().GetProperties()
            .Where(p => p.PropertyType == typeof(string)).Select(p => p.Name + "=" + p.GetValue(d))))));
    }

    [Fact]
    public async Task HostMigration_DeploysTheCompleteSqliteSchemaAndVersionSeed()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var sut = new CacheTestHostDbContext(new DbContextOptionsBuilder<CacheTestHostDbContext>().UseSqlite(connection, migration => migration.MigrationsAssembly(typeof(CacheTestHostDbContext).Assembly.GetName().Name)).Options);

        await sut.Database.MigrateAsync();

        var seed = await sut.Set<CacheStoreEntity>().SingleAsync();
        seed.MetadataFormat.ShouldBe(1);
        seed.IdentityFormat.ShouldBe(1);
        seed.Id.ShouldBe("root");
        sut.Model.GetEntityTypes().Count().ShouldBe(12);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name GLOB '__Caching_*'";
        Convert.ToInt32(await command.ExecuteScalarAsync()).ShouldBe(12);
    }

    [Fact]
    public void ModelCache_SeparatesValidatedPhysicalNames()
    {
        var options = new DbContextOptionsBuilder<CacheDbContext>().UseSqlite("Data Source=:memory:", migration => migration.MigrationsAssembly(typeof(CacheTestHostDbContext).Assembly.GetName().Name)).Options;
        using var first = new CacheDbContext(options);
        using var second = new CacheDbContext(options, new() { TablePrefix = "AltCache" });

        first.Model.ShouldNotBeSameAs(second.Model);
        first.Model.FindEntityType(typeof(CacheEntryEntity)).GetTableName().ShouldBe("__Caching_Entries");
        second.Model.FindEntityType(typeof(CacheEntryEntity)).GetTableName().ShouldBe("AltCacheEntries");
        Should.Throw<ArgumentException>(() => new CacheDbContext(options, new() { TablePrefix = "unsafe; DROP TABLE" }));
    }

    [Fact]
    public async Task CanonicalIdentities_AreBinaryColumnsIndependentOfDatabaseCollation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var sut = new CacheTestHostDbContext(new DbContextOptionsBuilder<CacheTestHostDbContext>().UseSqlite(connection, migration => migration.MigrationsAssembly(typeof(CacheTestHostDbContext).Assembly.GetName().Name)).Options);
        await sut.Database.MigrateAsync();
        var query = sut.Set<CacheStoreEntity>().Where(e => e.Id == "root");
        query.ToQueryString().ShouldContain("X'726F6F74'");

        var seed = await query.SingleAsync();
        seed.Id.ShouldBe("root");
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT typeof(Id), Id FROM __Caching_Stores";
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).ShouldBeTrue();
        reader.GetString(0).ShouldBe("blob");
        Encoding.ASCII.GetString((byte[])reader[1]).ShouldBe("root");
    }

    [Fact]
    public void MetadataProjection_DoesNotSelectPayloadColumns()
    {
        using var sut = new CacheDbContext(new DbContextOptionsBuilder<CacheDbContext>().UseSqlite("Data Source=:memory:", migration => migration.MigrationsAssembly(typeof(CacheTestHostDbContext).Assembly.GetName().Name)).Options);
        var id = "abcdef";
        var query = sut.Set<CacheEntryEntity>().AsNoTracking().Where(e => e.Id == id).Select(e => new { e.MetadataJson, e.ExpiryTicks });

        var sql = query.ToQueryString();

        sql.ShouldNotContain("InlineBytes");
        sql.ShouldNotContain("__Caching_Payloads");
        sql.ShouldNotContain("__Caching_Chunks");
        sql.ShouldContain("MetadataJson");
    }
}