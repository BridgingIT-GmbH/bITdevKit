// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Storage;

using Application.Storage;
using Infrastructure.EntityFramework;
using Infrastructure.EntityFramework.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public sealed partial class StorageDimensionsMutationTests
{
    [Fact]
    public async Task DocumentTranslation_TypedStatesAndGroups_ExecuteInSqlite()
    {
        var scopes = this.services.GetRequiredService<IServiceScopeFactory>();
        var client = new DocumentStoreClient<Invoice>(new EntityFrameworkDocumentStoreProvider<DimensionsContext>(scopes));
        object[] numbers = [1, 2, true, null, null, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 1.0m, 1];
        string[] labels = ["A", "a", "A ", "", "e\u0301", "é", "Z", null];
        for (var index = 0; index < numbers.Length; index++)
        {
            var values = new Dictionary<string, object> { ["label"] = labels[index] };
            if (index != 4) values["number"] = numbers[index];
            var written = await client.UpsertAsync(new("filters", index.ToString()), new Invoice { Amount = index },
                new() { Dimensions = DocumentDimensions.Create(values).Value });
            written.IsSuccess.ShouldBeTrue(string.Join(";", written.Errors.Select(error => error.Message)));
        }

        using var scope = this.services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DimensionsContext>();
        var resolver = new DocumentDimensionNameResolver<DimensionsContext>(scopes);
        var capabilities = new DocumentDimensionCapabilities { SupportsDimensions = true, SupportsQueries = true };
        await Check(Leaf("number", DocumentDimensionOperator.Equal, 1), "0", "7");
        await Check(Leaf("number", DocumentDimensionOperator.NotEqual, 1), "1");
        await Check(Leaf("number", DocumentDimensionOperator.GreaterThan, 1), "1");
        await Check(Leaf("number", DocumentDimensionOperator.Equal, true), "2");
        await Check(Leaf("number", DocumentDimensionOperator.Equal, 1.000000m), "6");
        await Check(Leaf("number", DocumentDimensionOperator.Equal, numbers[5]), "5");
        await Check(Leaf("number", DocumentDimensionOperator.Exists), "0", "1", "2", "3", "5", "6", "7");
        await Check(Leaf("number", DocumentDimensionOperator.Missing), "4");
        await Check(Leaf("number", DocumentDimensionOperator.IsNull), "3");
        await Check(Leaf("number", DocumentDimensionOperator.IsNotNull), "0", "1", "2", "5", "6", "7");
        await Check(Leaf("label", DocumentDimensionOperator.Equal, "A"), "0");
        await Check(Leaf("label", DocumentDimensionOperator.Equal, "A "), "2");
        await Check(Leaf("label", DocumentDimensionOperator.Equal, ""), "3");
        await Check(Leaf("label", DocumentDimensionOperator.Equal, "e\u0301"), "4");
        await Check(Leaf("label", DocumentDimensionOperator.Equal, "é"), "5");
        await Check(Leaf("unknown", DocumentDimensionOperator.Exists));
        await Check(Leaf("unknown", DocumentDimensionOperator.Missing), "0", "1", "2", "3", "4", "5", "6", "7");
        await Check(DocumentDimensionFilter.Any(Leaf("number", DocumentDimensionOperator.Equal, 1), Leaf("number", DocumentDimensionOperator.Equal, true)), "0", "2", "7");
        await Check(DocumentDimensionFilter.All(Leaf("number", DocumentDimensionOperator.Exists), Leaf("number", DocumentDimensionOperator.NotEqual, 1)), "1");
        var unsupported = await DocumentDimensionQueryTranslator.ApplyAsync(context, context.StorageDocuments.Where(owner => owner.PartitionKey == "filters"), DocumentTypeIdentity.For<Invoice>().Value,
            Leaf("number", DocumentDimensionOperator.GreaterThan, 1.0m), capabilities, resolver, CancellationToken.None);
        unsupported.IsFailure.ShouldBeTrue();

        async Task Check(DocumentDimensionFilter filter, params string[] expected)
        {
            var translated = await DocumentDimensionQueryTranslator.ApplyAsync(context, context.StorageDocuments.Where(owner => owner.PartitionKey == "filters"), DocumentTypeIdentity.For<Invoice>().Value,
                filter, capabilities, resolver, CancellationToken.None);
            translated.IsSuccess.ShouldBeTrue(string.Join(";", translated.Errors.Select(error => error.Message)));
            var actual = await translated.Value.OrderBy(owner => owner.RowKey).Select(owner => owner.RowKey).ToArrayAsync();
            actual.ShouldBe(expected);
        }

        static DocumentDimensionFilter Leaf(string name, DocumentDimensionOperator operation, object operand = null) =>
            DocumentDimensionFilter.Where(DocumentDimensionPredicate.Create(name, operation, operand).Value);
    }
}
