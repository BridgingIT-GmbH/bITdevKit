// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Application.Storage;
using Infrastructure.EntityFramework;
using Infrastructure.EntityFramework.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

public sealed partial class StorageDimensionsMutationProofTests
{
    [Fact]
    public async Task SqliteRetention_UsesIndexedUtcExpiry_AndCascadesDimensionsAtExactTickCutoff()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var services = new ServiceCollection().AddDbContext<MutationContext>(options => options.UseSqlite(connection)).BuildServiceProvider();
        await using (var scope = services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<MutationContext>().Database.EnsureCreatedAsync();
        var scopes = services.GetRequiredService<IServiceScopeFactory>();
        var provider = new EntityFrameworkBlobStoreProvider<MutationContext>(scopes);
        var blobs = new BlobStoreClient("ef", provider);
        var cutoff = new DateTimeOffset(2026, 11, 16, 20, 0, 0, TimeSpan.Zero).AddTicks(1234567);
        (string Name, DateTimeOffset? Expiry)[] records =
        [
            ("oldest", cutoff.AddTicks(-2).ToOffset(TimeSpan.FromHours(5.5))),
            ("past", cutoff.AddTicks(-1)), ("equal", cutoff),
            ("future", cutoff.AddTicks(1).ToOffset(TimeSpan.FromHours(-7))), ("permanent", null)
        ];
        foreach (var record in records)
        {
            using var content = new MemoryStream([1]);
            var uploaded = await blobs.UploadAsync(new()
            {
                Key = new("retention", record.Name), Content = content, ExpiresAt = record.Expiry,
                Dimensions = BlobValues(("Retained", 1))
            });
            uploaded.IsSuccess.ShouldBeTrue(Errors(uploaded));
        }

        var swept = await provider.SweepExpiredAsync(new() { ExpiresOnOrBefore = cutoff, BatchSize = 1, MaxBatches = 4 });
        swept.IsSuccess.ShouldBeTrue(Errors(swept));
        swept.Value.DeletedCount.ShouldBe(3);
        await using var verification = services.CreateAsyncScope();
        var context = verification.ServiceProvider.GetRequiredService<MutationContext>();
        (await context.StorageBlobs.Select(row => row.Name).OrderBy(name => name).ToListAsync()).ShouldBe(new[] { "future", "permanent" });
        (await context.Set<StorageBlobDimension>().CountAsync()).ShouldBe(2);
        (await context.Set<StorageBlobDimensionName>().CountAsync()).ShouldBe(1);

        using var planCommand = connection.CreateCommand();
        planCommand.CommandText = "EXPLAIN QUERY PLAN SELECT * FROM \"__Storage_Blobs\" WHERE \"ExpiresAt\" IS NOT NULL AND \"ExpiresAt\" <= $cutoff ORDER BY \"ExpiresAt\", \"Id\" LIMIT 1";
        planCommand.Parameters.AddWithValue("$cutoff", cutoff);
        var plan = new StringBuilder();
        await using (var reader = await planCommand.ExecuteReaderAsync()) while (await reader.ReadAsync()) plan.AppendLine(reader.GetString(3));
        var index = context.Model.FindEntityType(typeof(StorageBlob)).GetIndexes().Single(item => item.Properties.Count == 1 && item.Properties[0].Name == nameof(StorageBlob.ExpiresAt));
        plan.ToString().ShouldContain(index.GetDatabaseName());
        plan.ToString().ShouldContain("SEARCH");
    }
}
