// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Checks lazy factory-based registration and independently owned cache-capable module contexts.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~CacheFactoryRegistrationTests</code></example>
public sealed class CacheFactoryRegistrationTests
{
    /// <summary>Resolves neither factory nor contexts before native access and disposes a fresh factory-created context after each operation.</summary>
    [Fact]
    public async Task RegisteredFactory_IsLazyAndOwnsFreshModuleContextsPerNativeOperation()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), "cache-factory-registration-" + Guid.NewGuid().ToString("N") + ".db");
        var options = new DbContextOptionsBuilder<FactoryContext>()
            .UseSqlite("Data Source=" + path + ";Pooling=False;Default Timeout=5").Options;
        var factory = new TrackingFactory(options);
        var factoryResolutions = 0;
        var services = Services();
        services.AddSingleton<IDbContextFactory<FactoryContext>>(_ =>
        {
            factoryResolutions++;
            return factory;
        });
        services.AddScoped<FactoryContext>(_ => throw new InvalidOperationException("Cache operations cannot borrow request-scoped contexts."));
        try
        {
            await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            var native = provider.GetRequiredService<CacheProviderRegistry>().Get("database");
            var sut = provider.GetRequiredService<IApplicationCache>().GetCollection<string>("items");
            native.ShouldBeOfType<EntityFrameworkCacheStoreProvider<FactoryContext>>();
            factoryResolutions.ShouldBe(0);
            factory.Contexts.ShouldBeEmpty();
            File.Exists(path).ShouldBeFalse();

            await using (var bootstrap = new FactoryContext(options))
            {
                // Deployment belongs to the consumer; neither registration nor a cache request creates the schema.
                (await bootstrap.Database.EnsureCreatedAsync()).ShouldBeTrue();
                await bootstrap.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
                bootstrap.Model.FindEntityType(typeof(CacheEntryEntity)).GetTableName().ShouldBe("__Caching_Entries");
            }

            // Act
            var first = await sut.GetAsync("first");
            var second = await sut.GetAsync("second");

            // Assert
            first.IsSuccess.ShouldBeTrue(string.Join(", ", first.Errors.Select(error => error.Message)));
            second.IsSuccess.ShouldBeTrue(string.Join(", ", second.Errors.Select(error => error.Message)));
            first.Value.Found.ShouldBeFalse();
            second.Value.Found.ShouldBeFalse();
            factoryResolutions.ShouldBe(1);
            factory.Contexts.Count.ShouldBeGreaterThanOrEqualTo(3); // Initialization and both independent lookups.
            factory.Contexts.Distinct<FactoryContext>(ReferenceEqualityComparer.Instance).Count().ShouldBe(factory.Contexts.Count);
            factory.Contexts.ShouldAllBe(context => context.Disposed);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + "-wal");
            File.Delete(path + "-shm");
        }
    }

    /// <summary>A missing factory produces its DI error instead of resolving an available scoped module context.</summary>
    [Fact]
    public async Task MissingFactory_IsRequiredAndNeverFallsBackToAScopedContext()
    {
        // Arrange
        var services = Services();
        var scopedResolutions = 0;
        services.AddScoped(_ =>
        {
            scopedResolutions++;
            return new FactoryContext(new DbContextOptionsBuilder<FactoryContext>().UseSqlite("Data Source=:memory:").Options);
        });
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var sut = provider.GetRequiredService<CacheProviderRegistry>().Get("database");

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () => await sut.InitializeAsync([]));

        // Assert
        exception.Message.ShouldContain(nameof(IDbContextFactory<FactoryContext>));
        scopedResolutions.ShouldBe(0);
    }

    /// <summary>The factory shortcut preserves native engine checks and disposes the rejected independently created context.</summary>
    [Fact]
    public async Task FactoryContext_WithMismatchedEngine_RejectsCapabilitiesAndDisposesContext()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<FactoryContext>().UseSqlite("Data Source=:memory:").Options;
        var factory = new TrackingFactory(options);
        var services = Services("SqlServer");
        services.AddSingleton<IDbContextFactory<FactoryContext>>(factory);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var sut = provider.GetRequiredService<CacheProviderRegistry>().Get("database");

        // Act
        var result = await sut.InitializeAsync([]);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.OfType<CacheError>().ShouldHaveSingleItem().Code.ShouldBe(CacheErrorCode.Validation);
        factory.Contexts.ShouldHaveSingleItem().Disposed.ShouldBeTrue();
    }

    private static ServiceCollection Services(string engine = "Sqlite")
    {
        var services = new ServiceCollection();
        services.AddApplicationCaching()
            .WithEntityFrameworkProvider<FactoryContext>()
            .WithProvider("database", provider => provider.With(value => value.Kind, "EntityFramework").With(value => value.Engine, engine))
            .WithValueContract<string>("text")
            .WithCollection<string>("items", collection => collection.UseContract("text").UseProvider("database"));
        return services;
    }

    private sealed class TrackingFactory(DbContextOptions<FactoryContext> options) : IDbContextFactory<FactoryContext>
    {
        /// <summary>Gets the independently created contexts for disposal and identity assertions.</summary>
        public List<FactoryContext> Contexts { get; } = [];

        /// <inheritdoc />
        public FactoryContext CreateDbContext()
        {
            var context = new FactoryContext(options);
            this.Contexts.Add(context);
            try
            {
                context.Database.OpenConnection();
                using var command = context.Database.GetDbConnection().CreateCommand();
                command.CommandText = "PRAGMA busy_timeout=5000;";
                command.ExecuteNonQuery();
                return context;
            }
            catch { context.Dispose(); throw; }
        }
    }

    private sealed class FactoryContext : ModuleDbContextBase, IApplicationCachingContext
    {
        /// <summary>Creates an isolated consumer module context with automatic cache capability configuration.</summary>
        /// <param name="options">The independently owned context's native options.</param>
        public FactoryContext(DbContextOptions<FactoryContext> options) : base(options)
        {
            this.ApplyConfigurationsFromAssembly = false;
        }

        /// <summary>Gets whether the operation has disposed this context.</summary>
        public bool Disposed { get; private set; }

        /// <inheritdoc />
        public DbSet<CacheStoreEntity> CacheStores { get; set; }

        /// <inheritdoc />
        public DbSet<CacheCollectionEntity> CacheCollections { get; set; }

        /// <inheritdoc />
        public DbSet<CacheContractEntity> CacheContracts { get; set; }

        /// <inheritdoc />
        public DbSet<CacheKeyEntity> CacheKeys { get; set; }

        /// <inheritdoc />
        public DbSet<CacheEntryEntity> CacheEntries { get; set; }

        /// <inheritdoc />
        public DbSet<CachePayloadEntity> CachePayloads { get; set; }

        /// <inheritdoc />
        public DbSet<CacheChunkEntity> CacheChunks { get; set; }

        /// <inheritdoc />
        public DbSet<CacheDimensionEntity> CacheDimensions { get; set; }

        /// <inheritdoc />
        public DbSet<CacheFenceEntity> CacheFences { get; set; }

        /// <inheritdoc />
        public DbSet<CacheOwnershipEntity> CacheOwnerships { get; set; }

        /// <inheritdoc />
        public DbSet<CacheReaderEntity> CacheReaders { get; set; }

        /// <inheritdoc />
        public DbSet<CacheOperationEntity> CacheOperations { get; set; }

        /// <inheritdoc />
        public override void Dispose()
        {
            base.Dispose();
            this.Disposed = true;
        }

        /// <inheritdoc />
        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            this.Disposed = true;
        }
    }
}