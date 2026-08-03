// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Storage.Permalinks;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[UnitTest("Infrastructure")]
public sealed class EntityFrameworkStoragePermalinkRegistryProviderTests
{
    [Fact]
    public async Task Operations_WithDisposedCallerScope_UseProviderOwnedContexts()
    {
        var services = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString("N");
        services.AddDbContext<TestContext>(options => options.UseInMemoryDatabase(databaseName));
        using var root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var sut = new EntityFrameworkStoragePermalinkRegistryProvider<TestContext>(root.GetRequiredService<IServiceScopeFactory>());
        using (var callerScope = root.CreateScope()) { _ = callerScope.ServiceProvider.GetRequiredService<TestContext>(); }

        var created = await sut.GetOrCreateAsync(StorageResourceLocation.ForBlob("reports", new("public", "report.pdf")));
        created.IsSuccess.ShouldBeTrue(string.Join(" | ", created.Errors.Select(x => $"{x.Message} {(x as StoragePermalinkProviderError)?.InnerException}")));
        created.Value.Id.Value.ShouldNotBeNullOrWhiteSpace();
        using (var verificationScope = root.CreateScope())
        {
            (await verificationScope.ServiceProvider.GetRequiredService<TestContext>().StoragePermalinks.CountAsync()).ShouldBe(1);
        }
        var loaded = await sut.GetByIdAsync(created.Value.Id);

        created.IsSuccess.ShouldBeTrue();
        loaded.IsSuccess.ShouldBeTrue(string.Join(" | ", loaded.Errors.Select(x => $"{x.Message} {(x as StoragePermalinkProviderError)?.InnerException}")));
        loaded.Value.Location.Path.ShouldBe("report.pdf");
    }

    [Fact]
    public async Task UpdateAndDelete_WithMatchingEtags_ApplyMaintenanceChanges()
    {
        var services = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString("N");
        services.AddDbContext<TestContext>(options => options.UseInMemoryDatabase(databaseName));
        using var root = services.BuildServiceProvider();
        var sut = new EntityFrameworkStoragePermalinkRegistryProvider<TestContext>(root.GetRequiredService<IServiceScopeFactory>());
        var created = await sut.GetOrCreateAsync(StorageResourceLocation.ForFile("files", "report.pdf"));
        created.IsSuccess.ShouldBeTrue(string.Join(" | ", created.Errors.Select(x => $"{x.Message} {(x as StoragePermalinkProviderError)?.InnerException}")));

        var updated = await sut.UpdateExpirationAsync(created.Value.Id, new() { ExpiresAt = DateTimeOffset.UtcNow.AddDays(1), IfMatchETag = created.Value.ETag });
        updated.IsSuccess.ShouldBeTrue(string.Join(" | ", updated.Errors.Select(x => $"{x.Message} {(x as StoragePermalinkProviderError)?.InnerException}")));
        var deleted = await sut.DeleteAsync(created.Value.Id, new() { IfMatchETag = updated.Value.ETag });
        var loaded = await sut.GetByIdAsync(created.Value.Id);

        deleted.IsSuccess.ShouldBeTrue();
        loaded.Value.Status.ShouldBe(StoragePermalinkStatus.Deleted);
    }

    [Fact]
    public async Task MoveAsync_AfterExpirationMaintenance_UsesStorageChangeOrdering()
    {
        using var root = CreateProvider(out var sut);
        var source = StorageResourceLocation.ForFile("files", "incoming/report.pdf");
        var target = StorageResourceLocation.ForFile("files", "archive/report.pdf");
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-20);
        var created = await sut.GetOrCreateAsync(source, occurredAt: createdAt);
        await sut.UpdateExpirationAsync(created.Value.Id, new() { ExpiresAt = DateTimeOffset.UtcNow.AddDays(1) });

        var moved = await sut.MoveAsync(source, target, createdAt.AddMinutes(10));

        moved.IsSuccess.ShouldBeTrue(string.Join(" | ", moved.Errors.Select(x => x.Message)));
        moved.Value.Id.ShouldBe(created.Value.Id);
        (await sut.GetByLocationAsync(target)).Value.Id.ShouldBe(created.Value.Id);
    }

    [Fact]
    public async Task DeleteBeforeDelayedUpsert_PersistsSynchronizationTombstone()
    {
        using var root = CreateProvider(out var sut);
        var location = StorageResourceLocation.ForBlob("reports", new("public", "report.pdf"));
        var deletedAt = DateTimeOffset.UtcNow.AddMinutes(2);

        var deleted = await sut.DeleteByLocationAsync(location, deletedAt);
        deleted.IsSuccess.ShouldBeTrue(string.Join(" | ", deleted.Errors.Select(x => x.Message)));
        using (var scope = root.CreateScope())
        {
            var rows = await scope.ServiceProvider.GetRequiredService<TestContext>().StoragePermalinks.AsNoTracking().ToListAsync();
            rows.Count.ShouldBe(1);
            rows[0].IsSynchronizationTombstone.ShouldBeTrue();
            rows[0].UpdatedAt.ShouldBe(deletedAt);
        }
        var result = await sut.GetOrCreateAsync(location, occurredAt: deletedAt.AddMinutes(-1));

        result.Errors.ShouldContain(x => x is StoragePermalinkConflictError, string.Join(" | ", result.Errors.Select(x => $"{x.GetType().Name}: {x.Message} {(x as StoragePermalinkProviderError)?.InnerException}")));
    }

    [Fact]
    public async Task PrefixDeleteBeforeDelayedChildUpsert_PersistsSynchronizationTombstone()
    {
        using var root = CreateProvider(out var sut);
        var prefix = StorageResourceLocation.ForFile("files", "archive");
        var deletedAt = DateTimeOffset.UtcNow.AddMinutes(2);

        var deleted = await sut.DeletePrefixAsync(prefix, deletedAt);
        deleted.IsSuccess.ShouldBeTrue(string.Join(" | ", deleted.Errors.Select(x => x.Message)));
        var result = await sut.GetOrCreateAsync(StorageResourceLocation.ForFile("files", "archive/report.pdf"), occurredAt: deletedAt.AddMinutes(-1));

        result.Errors.ShouldContain(x => x is StoragePermalinkConflictError, string.Join(" | ", result.Errors.Select(x => $"{x.GetType().Name}: {x.Message} {(x as StoragePermalinkProviderError)?.InnerException}")));
    }

    [Fact]
    public async Task DelayedDelete_DoesNotRemoveNewerMapping()
    {
        using var root = CreateProvider(out var sut);
        var location = StorageResourceLocation.ForFile("files", "report.pdf");
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(2);
        var created = await sut.GetOrCreateAsync(location, occurredAt: createdAt);
        created.IsSuccess.ShouldBeTrue(string.Join(" | ", created.Errors.Select(x => x.Message)));

        var deleted = await sut.DeleteByLocationAsync(location, createdAt.AddMinutes(-1));
        deleted.IsSuccess.ShouldBeTrue(string.Join(" | ", deleted.Errors.Select(x => x.Message)));

        var loaded = await sut.GetByLocationAsync(location);
        loaded.IsSuccess.ShouldBeTrue(string.Join(" | ", loaded.Errors.Select(x => $"{x.GetType().Name}: {x.Message} {(x as StoragePermalinkProviderError)?.InnerException}")));
        loaded.Value.Id.ShouldBe(created.Value.Id);
    }

    [Fact]
    public async Task MovePrefix_WithExistingDestination_PreservesSourceIdentifierAndTombstonesDestination()
    {
        using var root = CreateProvider(out var sut);
        var now = DateTimeOffset.UtcNow;
        var source = await sut.GetOrCreateAsync(StorageResourceLocation.ForFile("files", "incoming/report.pdf"), occurredAt: now);
        var destination = await sut.GetOrCreateAsync(StorageResourceLocation.ForFile("files", "archive/report.pdf"), occurredAt: now);

        var result = await sut.MovePrefixAsync(StorageResourceLocation.ForFile("files", "incoming"), StorageResourceLocation.ForFile("files", "archive"), now.AddMinutes(1));

        result.IsSuccess.ShouldBeTrue(string.Join(" | ", result.Errors.Select(x => x.Message)));
        (await sut.GetByLocationAsync(StorageResourceLocation.ForFile("files", "archive/report.pdf"))).Value.Id.ShouldBe(source.Value.Id);
        (await sut.GetByIdAsync(destination.Value.Id)).Value.Status.ShouldBe(StoragePermalinkStatus.Deleted);
    }

    private static ServiceProvider CreateProvider(out EntityFrameworkStoragePermalinkRegistryProvider<TestContext> provider)
    {
        var services = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString("N");
        services.AddDbContext<TestContext>(options => options.UseInMemoryDatabase(databaseName));
        var root = services.BuildServiceProvider();
        provider = new(root.GetRequiredService<IServiceScopeFactory>());
        return root;
    }

    private sealed class TestContext(DbContextOptions<TestContext> options) : DbContext(options), IStoragePermalinkRegistryContext
    {
        public DbSet<StoragePermalink> StoragePermalinks { get; set; }
    }
}
