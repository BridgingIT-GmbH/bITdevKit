// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Checks typed relational provider declarations and metadata-only engine inference.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~CacheProviderFluentTests</code></example>
public sealed class CacheProviderFluentTests
{
    /// <summary>Late module registrations determine native capabilities without resolving a context or factory.</summary>
    /// <param name="databaseProvider">The existing DevKit context provider metadata.</param>
    /// <param name="engine">The expected native cache engine.</param>
    [Theory]
    [InlineData(Provider.SqlServer, "SqlServer")]
    [InlineData(Provider.Postgres, "PostgreSql")]
    [InlineData(Provider.Sqlite, "Sqlite")]
    public async Task NamedProvider_LateContextMetadata_InfersEngineWithoutNativeAccess(Provider databaseProvider, string engine)
    {
        // Arrange
        var services = new ServiceCollection();
        var factoryResolutions = 0;
        services.AddSingleton<IDbContextFactory<CacheDbContext>>(_ =>
        {
            factoryResolutions++;
            throw new InvalidOperationException("Catalog composition must not resolve a context factory.");
        });
        var sut = services.AddApplicationCaching().WithEntityFrameworkProvider<CacheDbContext>("database");
        services.AddDbContextRegistration<CacheDbContext>(databaseProvider);
        services.AddDbContextRegistration<DbContext>(Provider.SqlServer); // A different module cannot select this provider's engine.

        // Act
        var catalog = sut.BuildCatalog();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var native = provider.GetRequiredService<CacheProviderRegistry>().Get("database");

        // Assert
        catalog.Providers["database"].Options.Engine.ShouldBe(engine);
        catalog.Providers["database"].Options.DatabaseReadyAlias.ShouldBeNull();
        native.ShouldBeOfType<EntityFrameworkCacheStoreProvider<CacheDbContext>>();
        factoryResolutions.ShouldBe(0);
    }

    /// <summary>The normal DevKit SQL Server registration supplies engine metadata without executing its native-options callback.</summary>
    [Fact]
    public void NamedProvider_AddSqlServerDbContext_InfersEngineBeforeOptionsResolution()
    {
        var services = new ServiceCollection();
        var sut = services.AddApplicationCaching().WithEntityFrameworkProvider<CacheDbContext>("database");
        var nativeOptionsCalls = 0;
        services.AddSqlServerDbContext<CacheDbContext>(
            "Server=localhost;Database=TypedCaching;Integrated Security=true;TrustServerCertificate=true",
            _ => nativeOptionsCalls++).WithDbContextFactory();

        var catalog = sut.BuildCatalog();

        catalog.Providers["database"].Options.Engine.ShouldBe("SqlServer");
        nativeOptionsCalls.ShouldBe(0);
    }

    /// <summary>Missing matching metadata retains the established SQLite profile even when another module has SQL Server metadata.</summary>
    [Fact]
    public void NamedProvider_NoMatchingContextMetadata_DefaultsToSqlite()
    {
        var services = new ServiceCollection();
        services.AddDbContextRegistration<DbContext>(Provider.SqlServer);
        var sut = services.AddApplicationCaching().WithEntityFrameworkProvider<CacheDbContext>("database");

        sut.BuildCatalog().Providers["database"].Options.Engine.ShouldBe("Sqlite");
    }

    /// <summary>Explicit typed engines override metadata, while host configuration overrides that explicit choice.</summary>
    /// <param name="configured">Whether the host configuration supplies the final engine.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NamedProvider_EnginePrecedence_ConfigurationThenTypedThenMetadata(bool configured)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configured ?
            new Dictionary<string, string> { ["Providers:database:Engine"] = "Sqlite" } : []).Build();
        var services = new ServiceCollection();
        services.AddDbContextRegistration<CacheDbContext>(Provider.SqlServer);
        var sut = services.AddApplicationCaching().WithConfiguration(configuration)
            .WithEntityFrameworkProvider<CacheDbContext>("database", provider => provider.PostgreSql());

        sut.BuildCatalog().Providers["database"].Options.Engine.ShouldBe(configured ? "Sqlite" : "PostgreSql");
    }

    /// <summary>Typed native policies keep their values and derive a usable metadata default for a smaller resident budget.</summary>
    [Fact]
    public void NamedProvider_TypedSettings_PreservePoliciesAndScaleImplicitMetadata()
    {
        var sut = new ServiceCollection().AddApplicationCaching()
            .WithEntityFrameworkProvider<CacheDbContext>("database", provider => provider
                .Engine(CacheDatabaseEngine.SqlServer).DatabaseReadyAlias("catalog-ready")
                .MaxResidentBytes(8 * CacheDefaults.MiB).MaxStorageBytes(2 * CacheDefaults.GiB).MaxEntries(12000)
                .MetadataReserveRatio(0.08).AttemptTimeout(TimeSpan.FromSeconds(7))
                .InlinePayloadThreshold(32 * (int)CacheDefaults.KiB).ChunkSize(128 * (int)CacheDefaults.KiB)
                .LeaseLifetime(TimeSpan.FromSeconds(40)).LeaseRenewalInterval(TimeSpan.FromSeconds(12))
                .SqliteBusyTimeout(TimeSpan.FromSeconds(4)));

        var options = sut.BuildCatalog().Providers["database"].Options;

        options.Kind.ShouldBe("EntityFramework");
        options.Engine.ShouldBe("SqlServer");
        options.DatabaseReadyAlias.ShouldBe("catalog-ready");
        options.MaxResidentBytes.ShouldBe(8 * CacheDefaults.MiB);
        options.MaxStorageBytes.ShouldBe(2 * CacheDefaults.GiB);
        options.MaxEntries.ShouldBe(12000);
        options.MetadataCacheBytes.ShouldBe(CacheDefaults.MiB);
        options.MetadataReserveRatio.ShouldBe(0.08);
        options.AttemptTimeout.ShouldBe(TimeSpan.FromSeconds(7));
        options.InlinePayloadThreshold.ShouldBe(32 * (int)CacheDefaults.KiB);
        options.ChunkSize.ShouldBe(128 * (int)CacheDefaults.KiB);
        options.LeaseLifetime.ShouldBe(TimeSpan.FromSeconds(40));
        options.LeaseRenewalInterval.ShouldBe(TimeSpan.FromSeconds(12));
        options.SqliteBusyTimeout.ShouldBe(TimeSpan.FromSeconds(4));
    }

    /// <summary>Explicit invalid metadata budgets remain visible to final validation rather than being silently resized.</summary>
    [Fact]
    public void NamedProvider_ExplicitMetadataBeyondResident_IsRejected()
    {
        var sut = new ServiceCollection().AddApplicationCaching().WithEntityFrameworkProvider<CacheDbContext>("database",
            provider => provider.MetadataCacheBytes(16 * CacheDefaults.MiB).MaxResidentBytes(8 * CacheDefaults.MiB));

        Should.Throw<ArgumentException>(() => sut.BuildCatalog()).Message.ShouldContain("metadata cache");
    }

    /// <summary>Undefined enum values fail at the typed API boundary.</summary>
    [Fact]
    public void Engine_UndefinedValue_IsRejected()
    {
        var sut = new EntityFrameworkCacheProviderOptionsBuilder();

        Should.Throw<ArgumentOutOfRangeException>(() => sut.Engine((CacheDatabaseEngine)99));
    }
}