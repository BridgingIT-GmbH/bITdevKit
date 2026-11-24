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
    public async Task BlobTranslation_TypedStatesAndGroups_ExecuteInSqlite()
    {
        var scopes = this.services.GetRequiredService<IServiceScopeFactory>();
        var client = new BlobStoreClient("ef", new EntityFrameworkBlobStoreProvider<DimensionsContext>(scopes));
        object[] numbers = [1, 2, true, null, null, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 1.0m, 1];
        string[] labels = ["A", "a", "A ", "", "e\u0301", "é", "Z", null];
        for (var index = 0; index < numbers.Length; index++)
        {
            var values = new Dictionary<string, object> { ["label"] = labels[index] };
            if (index != 4) values["number"] = numbers[index];
            using var content = new MemoryStream([1]);
            var written = await client.UploadAsync(new() { Key = new("filters", index.ToString()), Content = content,
                Dimensions = BlobDimensions.Create(values).Value });
            written.IsSuccess.ShouldBeTrue(string.Join(";", written.Errors.Select(error => error.Message)));
        }

        using var scope = this.services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DimensionsContext>();
        var resolver = new BlobDimensionNameResolver<DimensionsContext>(scopes);
        var capabilities = new BlobDimensionCapabilities { SupportsDimensions = true, SupportsQueries = true };
        await Check(Leaf("number", BlobDimensionOperator.Equal, 1), "0", "7");
        await Check(Leaf("number", BlobDimensionOperator.NotEqual, 1), "1");
        await Check(Leaf("number", BlobDimensionOperator.GreaterThan, 1), "1");
        await Check(Leaf("number", BlobDimensionOperator.Equal, true), "2");
        await Check(Leaf("number", BlobDimensionOperator.Equal, 1.000000m), "6");
        await Check(Leaf("number", BlobDimensionOperator.Equal, numbers[5]), "5");
        await Check(Leaf("number", BlobDimensionOperator.Exists), "0", "1", "2", "3", "5", "6", "7");
        await Check(Leaf("number", BlobDimensionOperator.Missing), "4");
        await Check(Leaf("number", BlobDimensionOperator.IsNull), "3");
        await Check(Leaf("number", BlobDimensionOperator.IsNotNull), "0", "1", "2", "5", "6", "7");
        await Check(Leaf("label", BlobDimensionOperator.Equal, "A"), "0");
        await Check(Leaf("label", BlobDimensionOperator.Equal, "A "), "2");
        await Check(Leaf("label", BlobDimensionOperator.Equal, ""), "3");
        await Check(Leaf("label", BlobDimensionOperator.Equal, "e\u0301"), "4");
        await Check(Leaf("label", BlobDimensionOperator.Equal, "é"), "5");
        await Check(Leaf("unknown", BlobDimensionOperator.Exists));
        await Check(Leaf("unknown", BlobDimensionOperator.Missing), "0", "1", "2", "3", "4", "5", "6", "7");
        await Check(BlobDimensionFilter.Any(Leaf("number", BlobDimensionOperator.Equal, 1), Leaf("number", BlobDimensionOperator.Equal, true)), "0", "2", "7");
        await Check(BlobDimensionFilter.All(Leaf("number", BlobDimensionOperator.Exists), Leaf("number", BlobDimensionOperator.NotEqual, 1)), "1");
        var unsupported = await BlobDimensionQueryTranslator.ApplyAsync(context, context.StorageBlobs.Where(owner => owner.Container == "filters"), "filters",
            Leaf("number", BlobDimensionOperator.GreaterThan, 1.0m), capabilities, resolver, CancellationToken.None);
        unsupported.IsFailure.ShouldBeTrue();

        async Task Check(BlobDimensionFilter filter, params string[] expected)
        {
            var translated = await BlobDimensionQueryTranslator.ApplyAsync(context, context.StorageBlobs.Where(owner => owner.Container == "filters"), "filters",
                filter, capabilities, resolver, CancellationToken.None);
            translated.IsSuccess.ShouldBeTrue(string.Join(";", translated.Errors.Select(error => error.Message)));
            var actual = await translated.Value.OrderBy(owner => owner.Name).Select(owner => owner.Name).ToArrayAsync();
            actual.ShouldBe(expected);
        }

        static BlobDimensionFilter Leaf(string name, BlobDimensionOperator operation, object operand = null) =>
            BlobDimensionFilter.Where(BlobDimensionPredicate.Create(name, operation, operand).Value);
    }
}
