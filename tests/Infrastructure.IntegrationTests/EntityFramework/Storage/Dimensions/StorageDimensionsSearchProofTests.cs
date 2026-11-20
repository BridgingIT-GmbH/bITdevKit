namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Application.Storage;
using Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Data;
using System.Security.Cryptography;
using System.Text;

public sealed partial class StorageDimensionsDatabaseProofTests
{
    private static async Task VerifyPopulatedIndexesAsync(ProofContext sut, string engine, string directory, long blobAmountId, long docAmountId)
    {
        var blobNames = new List<StorageBlobDimensionName>();
        var documentNames = new List<StorageDocumentDimensionName>();
        for (var slot = 1; slot < 8; slot++)
        {
            blobNames.Add(new() { Scope = "archive", ScopeHash = SHA256.HashData("archive"u8), Name = "n" + slot, NameHash = SHA256.HashData(Encoding.UTF8.GetBytes("N" + slot)) });
            documentNames.Add(new() { Scope = "invoice", ScopeHash = SHA256.HashData("invoice"u8), Name = "n" + slot, NameHash = SHA256.HashData(Encoding.UTF8.GetBytes("N" + slot)) });
        }

        sut.AddRange(blobNames);
        sut.AddRange(documentNames);
        await sut.SaveChangesAsync();
        var blobIds = new[] { blobAmountId }.Concat(blobNames.Select(name => name.Id)).ToArray();
        var docIds = new[] { docAmountId }.Concat(documentNames.Select(name => name.Id)).ToArray();
        sut.ChangeTracker.Clear();
        sut.ChangeTracker.AutoDetectChangesEnabled = false;
        for (var start = 0; start < 20000; start += 1000)
        {
            for (var index = start; index < start + 1000; index++)
            {
                var blob = new StorageBlob { Container = "archive", ContainerHash = "container", Name = "b" + index.ToString("D8"), NameHash = "b" + index };
                var document = new StorageDocument { Id = Guid.CreateVersion7(), Type = "invoice", TypeHash = "type", PartitionKey = "p", PartitionKeyHash = "partition", RowKey = "d" + index.ToString("D8"), RowKeyHash = "d" + index };
                sut.AddRange(blob, document);
                for (var slot = 0; slot < 8; slot++)
                {
                    object scalar = slot switch
                    {
                        0 => index + 0.5m,
                        1 => index,
                        2 => index % 2 == 0,
                        3 => new DateTime(638000000000000000L + index, DateTimeKind.Utc),
                        4 => "C-" + index,
                        5 => null,
                        6 => index % 7,
                        _ => "é "
                    };
                    var blobValue = new StorageBlobDimension { BlobId = blob.Id, NameId = blobIds[slot] };
                    var docValue = new StorageDocumentDimension { DocumentId = document.Id, NameId = docIds[slot] };
                    BlobDimensionValueEncoding.Encode(BlobDimensionValue.Create(scalar).Value, blobValue);
                    DocumentDimensionValueEncoding.Encode(DocumentDimensionValue.Create(scalar).Value, docValue);
                    sut.AddRange(blobValue, docValue);
                }
            }

            await sut.SaveChangesAsync();
            sut.ChangeTracker.Clear();
        }

        sut.ChangeTracker.AutoDetectChangesEnabled = true;
        if (engine == "postgres") await sut.Database.ExecuteSqlRawAsync("ANALYZE");
        else if (engine == "sqlite") await sut.Database.ExecuteSqlRawAsync("ANALYZE");
        else
        {
            await sut.Database.ExecuteSqlRawAsync("UPDATE STATISTICS [__Storage_BlobDimensions]");
            await sut.Database.ExecuteSqlRawAsync("UPDATE STATISTICS [__Storage_DocumentDimensions]");
        }

        (await sut.Set<StorageBlobDimension>().CountAsync()).ShouldBe(160000);
        (await sut.Set<StorageDocumentDimension>().CountAsync()).ShouldBe(160000);
        foreach (var feature in new[] { "Blob", "Document" })
        {
            var ids = feature == "Blob" ? blobIds : docIds;
            var entity = sut.Model.FindEntityType(feature == "Blob" ? typeof(StorageBlobDimension) : typeof(StorageDocumentDimension));
            foreach (var slot in new[] { 0, 1, 3, 4 })
            {
                var column = slot == 0 ? "DecimalValue" : slot == 4 ? "StringValue" : "IntegralValue";
                var kind = slot switch { 0 => 2, 1 => 1, 3 => 5, _ => 4 };
                object operand = slot switch { 0 => 12345.500000m, 1 => 12345L, 3 => 638000000000012345L, _ => Convert.ToHexString("C-12345"u8) };
                var indexName = entity.GetIndexes().Single(index => index.Properties[2].Name == column).GetDatabaseName();
                var plan = await ExplainPredicateAsync(sut, engine, feature, ids[slot], kind, column, operand);
                await File.WriteAllTextAsync(Path.Combine(directory, feature + "-" + slot + "-plan.txt"), plan);
                plan.ShouldContain(indexName);
            }
        }

        foreach (var text in new[] { "", "é", "é ", "É ", "e\u0301 " })
        {
            var encoded = Convert.ToHexString(Encoding.UTF8.GetBytes(text));
            var blobNameId = blobIds[7];
            var docNameId = docIds[7];
            var expected = text == "é " ? 20000 : 0;
            (await sut.Set<StorageBlobDimension>().CountAsync(row => row.NameId == blobNameId && row.ValueType == BlobDimensionValueType.String && row.StringValue == encoded)).ShouldBe(expected);
            (await sut.Set<StorageDocumentDimension>().CountAsync(row => row.NameId == docNameId && row.ValueType == DocumentDimensionValueType.String && row.StringValue == encoded)).ShouldBe(expected);
        }

        await VerifyOwnerPageQueriesAsync(sut, engine, directory);

        // Verify actual provider translations on populated rows, including decimal scale canonicalization.
        var decimalOperand = 12345.500000m;
        (await sut.Set<StorageBlobDimension>().CountAsync(row => row.NameId == blobAmountId && row.ValueType == BlobDimensionValueType.Decimal && row.DecimalValue == decimalOperand)).ShouldBe(1);
        (await sut.Set<StorageDocumentDimension>().CountAsync(row => row.NameId == docAmountId && row.ValueType == DocumentDimensionValueType.Decimal && row.DecimalValue == decimalOperand)).ShouldBe(1);
    }

    private static async Task<string> ExplainPredicateAsync(ProofContext sut, string engine, string feature, long nameId, int kind, string column, object value)
    {
        await sut.Database.OpenConnectionAsync();
        await using var command = sut.Database.GetDbConnection().CreateCommand();
        var query = $"SELECT \"{feature}Id\" FROM \"__Storage_{feature}Dimensions\" WHERE \"NameId\" = @name AND \"ValueType\" = @kind AND \"{column}\" = @value";
        command.CommandText = engine switch
        {
            "postgres" => "EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) " + query,
            "sqlite" => "EXPLAIN QUERY PLAN " + query,
            _ => "SET STATISTICS XML ON; " + query + "; SET STATISTICS XML OFF;"
        };
        foreach (var entry in new[] { ("@name", (object)nameId, DbType.Int64), ("@kind", (object)(byte)kind, DbType.Byte), ("@value", value, value is decimal ? DbType.Decimal : value is string ? DbType.AnsiString : DbType.Int64) })
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = entry.Item1;
            parameter.Value = entry.Item2;
            parameter.DbType = entry.Item3;
            command.Parameters.Add(parameter);
        }

        var text = new StringBuilder();
        await using var reader = await command.ExecuteReaderAsync();
        do
        {
            while (await reader.ReadAsync())
                for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++) text.AppendLine(reader.GetValue(ordinal).ToString());
        } while (await reader.NextResultAsync());

        return text.ToString();
    }
}
