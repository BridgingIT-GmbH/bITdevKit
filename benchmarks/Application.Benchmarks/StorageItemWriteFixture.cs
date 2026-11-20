// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Benchmarks;

using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Application.Storage;
using Common;
using Infrastructure.EntityFramework;
using Infrastructure.EntityFramework.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Prepares and checks owned blob/document fixtures for item-write scaling measurements.</summary>
/// <param name="scopes">The service scopes for the run-owned database context.</param>
/// <example><code>var fixture = new StorageItemWriteFixture(scopes); await fixture.GrowAsync(0, 10000, "sqlserver");</code></example>
public sealed partial class StorageItemWriteFixture(IServiceScopeFactory scopes)
{
    private const string ScopeName = "write-scaling";
    private static readonly DocumentTypeIdentity DocumentType = new("ItemWriteRecord");
    private readonly EntityFrameworkBlobStoreProvider<DimensionsQualificationContext> blobs = new(scopes);
    private readonly EntityFrameworkDocumentStoreProvider<DimensionsQualificationContext> documents = new(scopes);

    private static string Key(int owner) => $"item-{owner:D8}";

    private static byte[] Payload(int revision)
    {
        var bytes = DimensionQualificationData.Payload.ToArray();
        System.Text.Encoding.ASCII.GetBytes(revision.ToString("X8")).CopyTo(bytes, 9);
        return bytes;
    }

    private static string Hash(byte[] bytes) => BlobContentHash.Prefix + Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static KeyValuePair<string, object>[] Values(int revision, bool active) =>
        [new("Number", revision), new("Active", active), new("Label", "value-" + revision)];

    private static T Require<T>(Result<T> result) => StorageDimensionsQualification.Require(result);

    private static Task SaveAsync(string output, string file, object value) =>
        File.WriteAllTextAsync(Path.Combine(output, file), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));

    /// <summary>Verifies durability and provisions the owned SQL transaction log outside measured work.</summary>
    /// <param name="engine">The sqlserver or postgres engine.</param>
    /// <param name="output">The evidence directory.</param>
    /// <example><code>await fixture.PrepareDatabaseAsync("postgres", output);</code></example>
    public async Task PrepareDatabaseAsync(string engine, string output)
    {
        await using var scope = scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DimensionsQualificationContext>();
        await context.Database.OpenConnectionAsync();
        using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandTimeout = 600;
        if (engine == "sqlserver")
        {
            command.CommandText = "SELECT name FROM sys.database_files WHERE type=1";
            var file = (string)await command.ExecuteScalarAsync();
            command.CommandText = "DECLARE @sql nvarchar(max)=N'ALTER DATABASE '+QUOTENAME(DB_NAME())+N' MODIFY FILE (NAME='+QUOTENAME(@file,N'''' )+N', SIZE=8192MB, FILEGROWTH=512MB)'; EXEC sys.sp_executesql @sql;";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@file";
            parameter.Value = file;
            command.Parameters.Add(parameter);
            await command.ExecuteNonQueryAsync();
            command.Parameters.Clear();
            command.CommandText = "SELECT @@VERSION AS Version, (SELECT COUNT(*) FROM sys.dm_os_schedulers WHERE status='VISIBLE ONLINE') AS Schedulers, (SELECT CONVERT(bigint,size)*8192 FROM sys.database_files WHERE type=1) AS LogBytes";
        }
        else
        {
            command.CommandText = "SELECT version() AS Version, current_setting('fsync') AS fsync, current_setting('synchronous_commit') AS synchronous_commit, current_setting('full_page_writes') AS full_page_writes";
        }

        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        var settings = Enumerable.Range(0, reader.FieldCount).ToDictionary(reader.GetName, index => reader.GetValue(index));
        if (engine == "sqlserver" && (Convert.ToInt32(settings["Schedulers"]) != 4 || Convert.ToInt64(settings["LogBytes"]) < 8192L * 1024 * 1024))
        {
            throw new InvalidOperationException("SQL budget/log verification failed.");
        }

        if (engine == "postgres" && new[] { "fsync", "synchronous_commit", "full_page_writes" }.Any(name => settings[name].ToString() != "on"))
        {
            throw new InvalidOperationException("PostgreSQL durability must remain enabled.");
        }

        await SaveAsync(output, "database-settings.json", settings);
    }

    /// <summary>Adds immutable seed records to both features outside measured provider calls.</summary>
    /// <param name="from">The existing population.</param>
    /// <param name="to">The target population.</param>
    /// <param name="engine">The sqlserver or postgres engine.</param>
    /// <example><code>await fixture.GrowAsync(10000, 100000, "sqlserver");</code></example>
    public async Task GrowAsync(int from, int to, string engine)
    {
        var content = Payload(0);
        var hash = Hash(content);
        var containerHash = HashHelper.ComputeSha256(ScopeName);
        var typeHash = HashHelper.ComputeSha256(DocumentType.Value);
        var partitionHash = HashHelper.ComputeSha256("records");
        await using var scope = scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DimensionsQualificationContext>();
        context.ChangeTracker.AutoDetectChangesEnabled = false;
        for (var start = from; start < to; start += 500)
        {
            for (var owner = start; owner < Math.Min(start + 500, to); owner++)
            {
                var key = Key(owner);
                var keyHash = HashHelper.ComputeSha256(key);
                var blob = new StorageBlob
                {
                    Container = ScopeName,
                    ContainerHash = containerHash,
                    Name = key,
                    NameHash = keyHash,
                    Length = content.Length,
                    ContentHash = hash,
                    ContentTypeMimeType = "application/json",
                    ETag = Guid.NewGuid().ToString("N")
                };
                blob.Chunks.Add(new()
                {
                    Blob = blob,
                    Index = 0,
                    Content = content,
                    Length = content.Length
                });
                context.Add(blob);
                context.Add(new StorageDocument
                {
                    Type = DocumentType.Value,
                    TypeHash = typeHash,
                    PartitionKey = "records",
                    PartitionKeyHash = partitionHash,
                    RowKey = key,
                    RowKeyHash = keyHash,
                    Content = content,
                    ContentHash = hash,
                    StoredContentHash = hash
                });
            }

            if (engine == "sqlserver")
            {
                await StorageDimensionsSqlFixture.WriteTrackedAsync(context);
            }
            else
            {
                await context.SaveChangesAsync();
            }

            context.ChangeTracker.Clear();
            if ((start + 500) % 100000 == 0 || start + 500 >= to)
            {
                Console.WriteLine($"Item write fixture: {Math.Min(start + 500, to):N0} records per feature.");
            }
        }
    }

    /// <summary>Prepares zero or three typed Dimensions on every owned fixture record outside timing.</summary>
    /// <param name="count">Zero or three.</param>
    /// <remarks>This replaces fixture snapshots and search rows. Use only run-owned test catalogs.</remarks>
    /// <example><code>await fixture.SetDimensionsAsync(3);</code></example>
    public async Task SetDimensionsAsync(int count)
    {
        if (count is not (0 or 3))
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (count == 3)
        {
            Require(await this.blobs.UpdateDimensionsAsync(new()
            {
                Key = new(ScopeName, Key(0)),
                Dimensions = Require(BlobDimensions.Create(Values(0, false)))
            }));
            Require(await this.documents.UpdateDimensionsAsync(DocumentType, new()
            {
                Key = new("records", Key(0)),
                Dimensions = Require(DocumentDimensions.Create(Values(0, false)))
            }));
        }

        await using var scope = scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DimensionsQualificationContext>();
        context.Database.SetCommandTimeout(600); // Fixture preparation is outside measured provider calls.
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"__Storage_BlobDimensions\"");
        await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"__Storage_DocumentDimensions\"");
        var blobJson = count == 0 ? null : BlobDimensionValues.Serialize(Require(BlobDimensions.Create(Values(0, false))));
        var documentJson = count == 0 ? null : DocumentDimensionValues.Serialize(Require(DocumentDimensions.Create(Values(0, false))));
        await context.StorageBlobs.Where(row => row.DimensionsJson != blobJson).ExecuteUpdateAsync(setters => setters.SetProperty(row => row.DimensionsJson, blobJson));
        await context.StorageDocuments.Where(row => row.DimensionsJson != documentJson).ExecuteUpdateAsync(setters => setters.SetProperty(row => row.DimensionsJson, documentJson));
        if (count == 3)
        {
            foreach (var pair in Values(0, false))
            {
                var encoded = new StorageBlobDimension();
                BlobDimensionValueEncoding.Encode(Require(BlobDimensionValue.Create(pair.Value)), encoded);
                foreach (var feature in new[] { "Blob", "Document" })
                {
                    using var command = context.Database.GetDbConnection().CreateCommand();
                    command.Transaction = transaction.GetDbTransaction();
                    command.CommandTimeout = 600;
                    var parent = feature == "Blob" ? "__Storage_Blobs" : "__Storage_Documents";
                    var names = feature == "Blob" ? "__Storage_BlobDimensionNames" : "__Storage_DocumentDimensionNames";
                    var values = feature == "Blob" ? "__Storage_BlobDimensions" : "__Storage_DocumentDimensions";
                    command.CommandText = $"INSERT INTO \"{values}\" (\"{feature}Id\", \"NameId\", \"ValueType\", \"IntegralValue\", \"DecimalValue\", \"StringValue\") SELECT p.\"Id\", n.\"Id\", @kind, @integral, NULL, @text FROM \"{parent}\" p CROSS JOIN \"{names}\" n WHERE n.\"Scope\"=@scope AND n.\"Name\"=@name";
                    foreach (var (name, type, value) in new[] { ("@kind", DbType.Byte, (object)(byte)encoded.ValueType), ("@integral", DbType.Int64, (object)encoded.IntegralValue), ("@text", DbType.String, (object)encoded.StringValue), ("@scope", DbType.String, (object)(feature == "Blob" ? ScopeName : DocumentType.Value)), ("@name", DbType.String, (object)pair.Key) })
                    {
                        var parameter = command.CreateParameter();
                        parameter.ParameterName = name;
                        parameter.DbType = type;
                        parameter.Value = value ?? DBNull.Value;
                        command.Parameters.Add(parameter);
                    }

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        await transaction.CommitAsync();
        this.blobActive.Clear();
        this.documentActive.Clear();
    }

    /// <summary>Checks owner, chunk, snapshot and typed-value populations before or after a measurement point.</summary>
    /// <param name="owners">Expected records per feature.</param>
    /// <param name="dimensions">Expected Dimensions per record, zero or three.</param>
    /// <example><code>await fixture.VerifyPopulationAsync(1000000, 3);</code></example>
    public async Task VerifyPopulationAsync(int owners, int dimensions)
    {
        await using var scope = scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DimensionsQualificationContext>();
        context.Database.SetCommandTimeout(600);
        if (await context.StorageBlobs.LongCountAsync() != owners || await context.StorageDocuments.LongCountAsync() != owners
            || await context.Set<StorageBlobChunk>().LongCountAsync() != owners
            || await context.Set<StorageBlobDimension>().LongCountAsync() != (long)owners * dimensions
            || await context.Set<StorageDocumentDimension>().LongCountAsync() != (long)owners * dimensions
            || await context.StorageBlobs.LongCountAsync(row => row.DimensionsJson != null) != (dimensions == 0 ? 0 : owners)
            || await context.StorageDocuments.LongCountAsync(row => row.DimensionsJson != null) != (dimensions == 0 ? 0 : owners))
        {
            throw new InvalidOperationException("Fixture owner/value/snapshot counts changed unexpectedly.");
        }

        foreach (var owner in new[] { 0, owners / 2, owners - 1 })
        {
            var blob = Require(await this.blobs.GetPropertiesAsync(new(ScopeName, Key(owner))));
            var doc = Require(await this.documents.GetAsync(DocumentType, new("records", Key(owner)), DateTimeOffset.UtcNow));
            if (blob.Dimensions.Count != dimensions || doc.Dimensions.Count != dimensions)
            {
                throw new InvalidOperationException("Fixture snapshot sampling failed.");
            }
        }
    }
}