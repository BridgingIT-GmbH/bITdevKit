// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using System.Collections.Concurrent;
using Application.IntegrationTests.Storage;
using Application.Storage;
using Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[IntegrationTest("Infrastructure")]
public class EntityFrameworkSqliteFileStorageHarnessTests(ITestOutputHelper output) : FileStorageTestsBase, IDisposable
{
    private readonly EntityFrameworkSqliteFileStorageTestSupport support = new(output);

    [Fact]
    public async Task RootDirectoryRow_ConcurrentFirstWrites_PersistsSingleRootRow()
    {
        using var harness = new SqliteFileHarness(output);
        var barrier = new Barrier(2);
        Func<Task> waitForPeer = () =>
        {
            barrier.SignalAndWait(TimeSpan.FromSeconds(5));
            return Task.CompletedTask;
        };

        var provider1 = harness.CreateProvider("race-files", waitForPeer);
        var provider2 = harness.CreateProvider("race-files", waitForPeer);

        var firstWrite = provider1.WriteFileAsync("alpha/file1.txt", new MemoryStream("one"u8.ToArray()), null, CancellationToken.None);
        var secondWrite = provider2.WriteFileAsync("beta/file2.txt", new MemoryStream("two"u8.ToArray()), null, CancellationToken.None);
        var results = await Task.WhenAll(firstWrite, secondWrite);

        using var scope = harness.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StubDbContext>();
        var rootRows = await dbContext.StorageDirectories
            .AsNoTracking()
            .Where(d => d.LocationName == "race-files" && d.NormalizedPath == string.Empty)
            .ToListAsync();

        results.ShouldAllBe(result => result.IsSuccess);
        rootRows.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RenameFileAsync_WhenSaveHitsTransientTimeout_ReturnsResourceUnavailableError()
    {
        const string locationName = "rename-timeout";
        var injectTimeout = false;
        var interceptor = new ThrowingSaveChangesInterceptor(
            context => injectTimeout &&
                context.ChangeTracker.Entries<FileStorageFileEntity>().Any(entry =>
                    entry.Entity.LocationName == locationName &&
                    entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted),
            () => throw new DbUpdateException("Simulated transient timeout", new TimeoutException("database is locked")));
        using var harness = new SqliteFileHarness(output, interceptor);
        var provider = harness.CreateProvider(locationName);
        await provider.WriteFileAsync("content/source.txt", new MemoryStream("payload"u8.ToArray()), null, CancellationToken.None);
        injectTimeout = true;

        var result = await provider.RenameFileAsync("content/source.txt", "content/renamed.txt", null, CancellationToken.None);

        result.ShouldBeFailure();
        result.ShouldContainError<ResourceUnavailableError>();
    }

    [Fact]
    public async Task CreateDirectoryAsync_ConcurrentSamePath_PersistsSingleDirectoryRowAndRemainsIdempotent()
    {
        const string locationName = "same-path-directory";
        const string parentPath = "contention/shared";
        const string path = "contention/shared/leaf";
        var competingCreateInjected = 0;
        string databasePath = null;
        ILoggerFactory competingLoggerFactory = null;
        var interceptor = new CoordinatedSaveChangesInterceptor(
            context => HasPendingAddedDirectory(context, locationName, path),
            async () =>
            {
                if (Interlocked.Exchange(ref competingCreateInjected, 1) != 0)
                {
                    return;
                }

                using var competingScope = CreateCompetingProviderScope(
                    databasePath,
                    competingLoggerFactory,
                    locationName);
                var competingResult = await competingScope.Provider.CreateDirectoryAsync(path, CancellationToken.None);
                competingResult.ShouldBeSuccess();
            });
        using var harness = new SqliteFileHarness(output, interceptor);
        databasePath = harness.DatabasePath;
        competingLoggerFactory = harness.LoggerFactory;
        var provider = harness.CreateProvider(locationName);

        (await provider.CreateDirectoryAsync(parentPath, CancellationToken.None)).ShouldBeSuccess();

        var result = await provider.CreateDirectoryAsync(path, CancellationToken.None);

        result.ShouldBeSuccess();
        await this.AssertDirectoryExistsAsync(provider, path);

        using var scope = harness.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StubDbContext>();
        var persistedDirectories = await dbContext.StorageDirectories
            .AsNoTracking()
            .Where(d => d.LocationName == locationName && d.NormalizedPath == path)
            .ToListAsync();

        persistedDirectories.Count.ShouldBe(1);
        persistedDirectories.Single().IsExplicit.ShouldBeTrue();
    }

    protected override IFileStorageProvider CreateProvider()
        => this.support.CreateProvider($"SqliteHarnessFiles-{Guid.NewGuid():N}");

    public void Dispose()
    {
        this.support.Dispose();
    }

    private static bool HasPendingAddedDirectory(DbContext context, string locationName, string normalizedPath)
        => context.ChangeTracker.Entries<FileStorageDirectoryEntity>()
            .Any(entry =>
                entry.State == EntityState.Added &&
                entry.Entity.LocationName == locationName &&
                entry.Entity.NormalizedPath == normalizedPath);

    private static CompetingProviderScope CreateCompetingProviderScope(
        string databasePath,
        ILoggerFactory loggerFactory,
        string locationName)
    {
        var services = new ServiceCollection();
        services.AddSingleton(loggerFactory);
        services.AddDbContext<StubDbContext>(options => options.UseSqlite($"Data Source={databasePath}"));
        var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var provider = new EntityFrameworkFileStorageProvider<StubDbContext>(
            serviceProvider,
            loggerFactory,
            locationName,
            options: EntityFrameworkSqliteFileStorageTestSupport.CreateOptions());

        return new CompetingProviderScope(serviceProvider, provider);
    }

    private sealed class SqliteFileHarness : IDisposable
    {
        public SqliteFileHarness(ITestOutputHelper output, params IInterceptor[] interceptors)
        {
            this.DatabasePath = Path.Combine(AppContext.BaseDirectory, $"entity-framework-file-storage-mutations-{Guid.NewGuid():N}.db");
            this.LoggerFactory = XunitLoggerFactory.Create(output);

            var services = new ServiceCollection();
            services.AddSingleton(this.LoggerFactory);
            services.AddDbContext<StubDbContext>(options =>
            {
                options.UseSqlite($"Data Source={this.DatabasePath}");

                if (interceptors is { Length: > 0 })
                {
                    options.AddInterceptors(interceptors);
                }
            });
            this.ServiceProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

            using var scope = this.ServiceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<StubDbContext>();
            dbContext.Database.EnsureDeleted();
            dbContext.Database.EnsureCreated();
        }

        public string DatabasePath { get; }

        public ILoggerFactory LoggerFactory { get; }

        public ServiceProvider ServiceProvider { get; }

        public EntityFrameworkFileStorageProvider<StubDbContext> CreateProvider(string locationName, Func<Task> beforeRootSaveAsync = null)
            => new TestEntityFrameworkFileStorageProvider(
                this.ServiceProvider,
                this.LoggerFactory,
                locationName,
                EntityFrameworkSqliteFileStorageTestSupport.CreateOptions(),
                beforeRootSaveAsync);

        public void Dispose()
        {
            this.ServiceProvider.Dispose();
            this.LoggerFactory.Dispose();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            try
            {
                if (File.Exists(this.DatabasePath))
                {
                    File.Delete(this.DatabasePath);
                }
            }
            catch (IOException)
            {
            }
        }
    }

    private sealed class CompetingProviderScope(
        ServiceProvider serviceProvider,
        EntityFrameworkFileStorageProvider<StubDbContext> provider) : IDisposable
    {
        public EntityFrameworkFileStorageProvider<StubDbContext> Provider { get; } = provider;

        public void Dispose()
        {
            serviceProvider.Dispose();
        }
    }

    private sealed class TestEntityFrameworkFileStorageProvider(
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory,
        string locationName,
        EntityFrameworkFileStorageOptions options = null,
        Func<Task> beforeRootSaveAsync = null)
        : EntityFrameworkFileStorageProvider<StubDbContext>(serviceProvider, loggerFactory, locationName, options: options)
    {
        private int rootHookInvoked;

        protected override async Task<FileStorageDirectoryEntity> EnsureRootDirectoryAsync(
            StubDbContext context,
            CancellationToken cancellationToken = default)
        {
            if (beforeRootSaveAsync is not null && Interlocked.Exchange(ref this.rootHookInvoked, 1) == 0)
            {
                await beforeRootSaveAsync();
            }

            return await base.EnsureRootDirectoryAsync(context, cancellationToken);
        }
    }

    private sealed class CoordinatedSaveChangesInterceptor(
        Func<DbContext, bool> shouldCoordinate,
        Func<Task> beforeSaveAsync) : SaveChangesInterceptor
    {
        private readonly ConcurrentDictionary<Guid, byte> coordinatedContexts = new();

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var context = eventData.Context;
            if (context is not null &&
                shouldCoordinate(context) &&
                this.coordinatedContexts.TryAdd(context.ContextId.InstanceId, 0))
            {
                await beforeSaveAsync();
            }

            return result;
        }
    }

    private sealed class ThrowingSaveChangesInterceptor(
        Func<DbContext, bool> shouldThrow,
        Action throwAction) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context is not null && shouldThrow(eventData.Context))
            {
                throwAction();
            }

            return ValueTask.FromResult(result);
        }
    }
}
