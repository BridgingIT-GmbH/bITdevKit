// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Storage.Permalinks;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[IntegrationTest("Infrastructure")]
public sealed class EntityFrameworkStoragePermalinkRegistryProviderTests
{
    [Fact]
    public async Task Move_WithExistingDestination_ReassignsUniqueActiveLocationAtomically()
    {
        await using var harness = await CreateHarnessAsync();
        var sut = harness.Provider;
        var now = DateTimeOffset.UtcNow;
        var sourceLocation = StorageResourceLocation.ForFile("files", "incoming/report.pdf");
        var targetLocation = StorageResourceLocation.ForFile("files", "archive/report.pdf");
        var source = await sut.GetOrCreateAsync(sourceLocation, occurredAt: now);
        var destination = await sut.GetOrCreateAsync(targetLocation, occurredAt: now);

        var moved = await sut.MoveAsync(sourceLocation, targetLocation, now.AddMinutes(1));

        moved.IsSuccess.ShouldBeTrue(string.Join(" | ", moved.Errors.Select(x => x.Message)));
        (await sut.GetByLocationAsync(targetLocation)).Value.Id.ShouldBe(source.Value.Id);
        (await sut.GetByIdAsync(destination.Value.Id)).Value.Status.ShouldBe(StoragePermalinkStatus.Deleted);
    }

    [Fact]
    public async Task MovePrefix_WithExistingDestination_ReassignsUniqueActiveLocationAtomically()
    {
        await using var harness = await CreateHarnessAsync();
        var sut = harness.Provider;
        var now = DateTimeOffset.UtcNow;
        var source = await sut.GetOrCreateAsync(StorageResourceLocation.ForFile("files", "incoming/report.pdf"), occurredAt: now);
        var destination = await sut.GetOrCreateAsync(StorageResourceLocation.ForFile("files", "archive/report.pdf"), occurredAt: now);

        var moved = await sut.MovePrefixAsync(
            StorageResourceLocation.ForFile("files", "incoming"),
            StorageResourceLocation.ForFile("files", "archive"),
            now.AddMinutes(1));

        moved.IsSuccess.ShouldBeTrue(string.Join(" | ", moved.Errors.Select(x => x.Message)));
        source.IsSuccess.ShouldBeTrue(string.Join(" | ", source.Errors.Select(x => x.Message)));
        destination.IsSuccess.ShouldBeTrue(string.Join(" | ", destination.Errors.Select(x => x.Message)));
        var loaded = await sut.GetByLocationAsync(StorageResourceLocation.ForFile("files", "archive/report.pdf"));
        loaded.IsSuccess.ShouldBeTrue(string.Join(" | ", loaded.Errors.Select(x => x.Message)));
        loaded.Value.Id.ShouldBe(source.Value.Id);
        (await sut.GetByIdAsync(destination.Value.Id)).Value.Status.ShouldBe(StoragePermalinkStatus.Deleted);
    }

    private static async Task<Harness> CreateHarnessAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddDbContext<TestContext>(options => options.UseSqlite(connection));
        var root = services.BuildServiceProvider();
        await using (var scope = root.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<TestContext>().Database.EnsureCreatedAsync();
        }

        return new(connection, root, new(root.GetRequiredService<IServiceScopeFactory>()));
    }

    private sealed class TestContext(DbContextOptions<TestContext> options) : DbContext(options), IStoragePermalinkRegistryContext
    {
        public DbSet<StoragePermalink> StoragePermalinks { get; set; }
    }

    private sealed class Harness(SqliteConnection connection, ServiceProvider root, EntityFrameworkStoragePermalinkRegistryProvider<TestContext> provider) : IAsyncDisposable
    {
        public EntityFrameworkStoragePermalinkRegistryProvider<TestContext> Provider { get; } = provider;

        public async ValueTask DisposeAsync()
        {
            await root.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
