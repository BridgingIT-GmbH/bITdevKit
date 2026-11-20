// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Application.Storage;
using Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Xml.Linq;
using System.Text.Json;

public sealed partial class StorageDimensionsDatabaseProofTests
{
    private static async Task VerifyOwnerPageQueriesAsync(ProofContext context, string engine, string directory)
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var scopes = services.GetRequiredService<IServiceScopeFactory>();

        var blobResolver = new BlobDimensionNameResolver<ProofContext>(scopes);
        var blobCapabilities = new BlobDimensionCapabilities { SupportsDimensions = true, SupportsQueries = true, SupportsDecimalComparisons = engine != "sqlite" };
        var blobOwners = context.Set<StorageBlob>().AsNoTracking().Where(owner => owner.Container == "archive");
        BlobDimensionFilter blobLeaf(string name, BlobDimensionOperator operation, object value) => BlobDimensionFilter.Where(BlobDimensionPredicate.Create(name, operation, value).Value);
        BlobDimensionFilter[] blobFilters = [
            blobLeaf("n1", BlobDimensionOperator.Equal, 12345),
            blobLeaf("amount", BlobDimensionOperator.Equal, 12345.500000m),
            BlobDimensionFilter.All(blobLeaf("n1", BlobDimensionOperator.GreaterThanOrEqual, 12300), blobLeaf("n1", BlobDimensionOperator.LessThan, 12500)),
            BlobDimensionFilter.Any(blobLeaf("n1", BlobDimensionOperator.Equal, 12345), blobLeaf("n4", BlobDimensionOperator.Equal, "C-5432")),
            BlobDimensionFilter.All(blobLeaf("n1", BlobDimensionOperator.Equal, 12345), blobLeaf("n6", BlobDimensionOperator.NotEqual, 0)),
            BlobDimensionFilter.All(blobLeaf("n1", BlobDimensionOperator.Equal, 12345), blobLeaf("Unregistered", BlobDimensionOperator.Missing, null)),
            BlobDimensionFilter.All(blobLeaf("n1", BlobDimensionOperator.Equal, 12345),
                BlobDimensionFilter.Any(blobLeaf("Unregistered", BlobDimensionOperator.Missing, null), blobLeaf("n6", BlobDimensionOperator.NotEqual, 0))),
            BlobDimensionFilter.Any(blobLeaf("n1", BlobDimensionOperator.Equal, 12345),
                BlobDimensionFilter.All(blobLeaf("n1", BlobDimensionOperator.GreaterThanOrEqual, 12345), blobLeaf("n1", BlobDimensionOperator.LessThan, 12346))),
            BlobDimensionFilter.Any(blobLeaf("n1", BlobDimensionOperator.Equal, 12345), blobLeaf("Unregistered", BlobDimensionOperator.Equal, 12345))
        ];
        for (var index = 0; index < blobFilters.Length; index++)
        {
            var translated = await BlobDimensionQueryTranslator.ApplyAsync(context, blobOwners, "archive", blobFilters[index], blobCapabilities, blobResolver, CancellationToken.None);
            translated.IsSuccess.ShouldBeTrue(string.Join(";", translated.Errors.Select(error => error.Message)));
            var query = translated.Value.OrderBy(owner => owner.Name).Select(owner => owner.Name).Take(100);
            if (index >= 5) query.ToQueryString().ShouldNotContain("INTERSECT");
            var rows = await query.ToArrayAsync();
            rows.Length.ShouldBe(index == 2 ? 100 : index == 3 ? 2 : 1);
            var plan = await ExplainOwnerPageAsync(context, query, engine);
            await File.WriteAllTextAsync(Path.Combine(directory, "Blob-owner-" + index + "-plan.txt"), plan);
            var integralIndex = context.Model.FindEntityType(typeof(StorageBlobDimension)).GetIndexes().Single(item => item.Properties[2].Name == (index == 1 ? "DecimalValue" : "IntegralValue")).GetDatabaseName();
            plan.ShouldContain(integralIndex);
            AssertNoParentScan(plan, engine);
        }

        var documentResolver = new DocumentDimensionNameResolver<ProofContext>(scopes);
        var documentCapabilities = new DocumentDimensionCapabilities { SupportsDimensions = true, SupportsQueries = true, SupportsDecimalComparisons = engine != "sqlite" };
        var documentOwners = context.Set<StorageDocument>().AsNoTracking().Where(owner => owner.TypeHash == "type" && owner.Type == "invoice");
        DocumentDimensionFilter documentLeaf(string name, DocumentDimensionOperator operation, object value) => DocumentDimensionFilter.Where(DocumentDimensionPredicate.Create(name, operation, value).Value);
        DocumentDimensionFilter[] documentFilters = [
            documentLeaf("n1", DocumentDimensionOperator.Equal, 12345),
            documentLeaf("amount", DocumentDimensionOperator.Equal, 12345.500000m),
            DocumentDimensionFilter.All(documentLeaf("n1", DocumentDimensionOperator.GreaterThanOrEqual, 12300), documentLeaf("n1", DocumentDimensionOperator.LessThan, 12500)),
            DocumentDimensionFilter.Any(documentLeaf("n1", DocumentDimensionOperator.Equal, 12345), documentLeaf("n4", DocumentDimensionOperator.Equal, "C-5432")),
            DocumentDimensionFilter.All(documentLeaf("n1", DocumentDimensionOperator.Equal, 12345), documentLeaf("n6", DocumentDimensionOperator.NotEqual, 0)),
            DocumentDimensionFilter.All(documentLeaf("n1", DocumentDimensionOperator.Equal, 12345), documentLeaf("Unregistered", DocumentDimensionOperator.Missing, null)),
            DocumentDimensionFilter.All(documentLeaf("n1", DocumentDimensionOperator.Equal, 12345),
                DocumentDimensionFilter.Any(documentLeaf("Unregistered", DocumentDimensionOperator.Missing, null), documentLeaf("n6", DocumentDimensionOperator.NotEqual, 0))),
            DocumentDimensionFilter.Any(documentLeaf("n1", DocumentDimensionOperator.Equal, 12345),
                DocumentDimensionFilter.All(documentLeaf("n1", DocumentDimensionOperator.GreaterThanOrEqual, 12345), documentLeaf("n1", DocumentDimensionOperator.LessThan, 12346))),
            DocumentDimensionFilter.Any(documentLeaf("n1", DocumentDimensionOperator.Equal, 12345), documentLeaf("Unregistered", DocumentDimensionOperator.Equal, 12345))
        ];
        for (var index = 0; index < documentFilters.Length; index++)
        {
            var translated = await DocumentDimensionQueryTranslator.ApplyAsync(context, documentOwners, "invoice", documentFilters[index], documentCapabilities, documentResolver, CancellationToken.None);
            translated.IsSuccess.ShouldBeTrue(string.Join(";", translated.Errors.Select(error => error.Message)));
            var query = translated.Value.OrderBy(owner => owner.RowKey).Select(owner => owner.RowKey).Take(100);
            if (index >= 5) query.ToQueryString().ShouldNotContain("INTERSECT");
            var rows = await query.ToArrayAsync();
            rows.Length.ShouldBe(index == 2 ? 100 : index == 3 ? 2 : 1);
            var plan = await ExplainOwnerPageAsync(context, query, engine);
            await File.WriteAllTextAsync(Path.Combine(directory, "Document-owner-" + index + "-plan.txt"), plan);
            var integralIndex = context.Model.FindEntityType(typeof(StorageDocumentDimension)).GetIndexes().Single(item => item.Properties[2].Name == (index == 1 ? "DecimalValue" : "IntegralValue")).GetDatabaseName();
            plan.ShouldContain(integralIndex);
            AssertNoParentScan(plan, engine);
        }

    }

    private static void AssertNoParentScan(string plan, string engine)
    {
        if (engine == "postgres")
        {
            using var json = JsonDocument.Parse(plan);
            Visit(json.RootElement[0].GetProperty("Plan"));
            return;

            static void Visit(JsonElement node)
            {
                if (node.TryGetProperty("Relation Name", out var relation) && relation.GetString() is "__Storage_Blobs" or "__Storage_Documents")
                    node.GetProperty("Node Type").GetString().ShouldNotBe("Seq Scan");
                if (node.TryGetProperty("Plans", out var children)) foreach (var child in children.EnumerateArray()) Visit(child);
            }
        }

        if (engine != "sqlserver") return;
        var document = XDocument.Parse(plan[plan.IndexOf("<ShowPlanXML", StringComparison.Ordinal)..]);
        XNamespace schema = "http://schemas.microsoft.com/sqlserver/2004/07/showplan";
        foreach (var owner in document.Descendants(schema + "Object").Where(node => (string)node.Attribute("Table") is "[__Storage_Blobs]" or "[__Storage_Documents]"))
        {
            var operation = owner.Ancestors(schema + "RelOp").First();
            ((string)operation.Attribute("PhysicalOp")).ShouldNotContain("Scan");
        }
    }

    private static async Task<string> ExplainOwnerPageAsync<T>(ProofContext context, IQueryable<T> query, string engine)
    {
        await context.Database.OpenConnectionAsync();
        await using var command = query.CreateDbCommand();
        command.CommandText = engine switch
        {
            "postgres" => "EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) " + command.CommandText,
            "sqlite" => "EXPLAIN QUERY PLAN " + command.CommandText,
            _ => "SET STATISTICS XML ON; " + command.CommandText + "; SET STATISTICS XML OFF;"
        };
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
