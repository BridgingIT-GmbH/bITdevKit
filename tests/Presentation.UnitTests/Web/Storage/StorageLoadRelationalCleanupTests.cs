// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using System.Text.Json;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;
using BridgingIT.DevKit.Presentation.Web.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[Collection(ProcessWideStateTestCollection.Name)]
public sealed class StorageLoadRelationalCleanupTests
{
    [Fact]
    public async Task ExecuteAsync_EntityFrameworkBlobProvider_RemovesOwnedRowsAfterPropertyRoundTrips()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddDbContext<LoadBlobContext>(options => options.UseSqlite(connection));
        collection.AddBlobStorage().WithInMemoryClient("reports");
        collection.AddKeyedSingleton<IBlobStoreProvider>("reports", (services, _) =>
            new EntityFrameworkBlobStoreProvider<LoadBlobContext>(services.GetRequiredService<IServiceScopeFactory>()));
        await using var services = collection.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<LoadBlobContext>().Database.EnsureCreatedAsync();
        }

        var provider = services.GetRequiredKeyedService<IBlobStoreProvider>("reports");
        (await provider.UploadAsync(new() { Key = new("reports", "keep"), Content = new MemoryStream([1, 2, 3]) })).IsSuccess.ShouldBeTrue();
        using var sut = new StorageLoadTest("blobs", services.GetRequiredService<IServiceScopeFactory>());

        var result = JsonSerializer.SerializeToElement(await sut.ExecuteAsync(new()
        {
            Target = "reports", Container = "reports", PayloadKiB = 4, Seconds = 1, ReadPercent = 50
        }, default));

        result.GetProperty("Phase").GetString().ShouldBe("Completed");
        result.GetProperty("cleanupPending").GetInt32().ShouldBe(0);
        result.GetProperty("errors").GetInt64().ShouldBe(0);
        (await provider.ExistsAsync(new("reports", "keep"))).Value.ShouldBeTrue();
        var remaining = await provider.ListPageAsync(new() { Container = "reports", Prefix = "bdk-diag-" });
        remaining.IsSuccess.ShouldBeTrue();
        remaining.Value.Items.ShouldBeEmpty();
    }

    private sealed class LoadBlobContext(DbContextOptions<LoadBlobContext> options) : DbContext(options), IBlobStoreContext
    {
        public DbSet<StorageBlob> StorageBlobs { get; set; }

        public DbSet<StorageBlobChunk> StorageBlobChunks { get; set; }
    }
}