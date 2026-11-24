// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Application.Storage;
using Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public sealed partial class StorageDimensionsMutationProofTests
{
    private static async Task VerifyBatchAndCardinalityAsync(ServiceProvider services, BlobStoreClient blobs, DocumentStoreClient<Receipt> documents)
    {
        var key = new DocumentKey("batch", "first");
        var batch = await documents.UpsertManyAsync([
            new(key, new() { Amount = 1 }, new() { Dimensions = DocumentValues(("BatchName", true)) }),
            new(new("customer", "receipt"), new() { Amount = 2 }, new() { CreateOnly = true, Dimensions = DocumentValues(("NotInCommittedPrefix", true)) }),
            new(new("batch", "last"), new() { Amount = 3 }, new() { Dimensions = DocumentValues(("NotReached", true)) })
        ]);
        batch.IsSuccess.ShouldBeTrue(Errors(batch));
        batch.Value.Items.Count.ShouldBe(1);
        batch.Value.FailedKey.ShouldBe(new DocumentKey("customer", "receipt"));
        (await documents.GetAsync(key)).Value.ETag.ShouldBe(batch.Value.Items[0].ETag);
        var duplicate = await documents.UpsertManyAsync([
            new(key, new() { Amount = 4 }, new() { Dimensions = DocumentValues(("BatchName", 4)) }),
            new(key, new() { Amount = 5 }, new() { Dimensions = DocumentValues(("BatchName", 5)) })
        ]);
        duplicate.IsSuccess.ShouldBeTrue(Errors(duplicate));
        duplicate.Value.Items.Count.ShouldBe(2);
        duplicate.Value.Items[0].ETag.ShouldNotBe(duplicate.Value.Items[1].ETag);
        (await documents.GetAsync(key)).Value.ETag.ShouldBe(duplicate.Value.Items[1].ETag);
        (await documents.DeleteAsync(key)).IsSuccess.ShouldBeTrue();
        await using (var scope = services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MutationContext>();
            (await context.Set<StorageDocumentDimensionName>().AnyAsync(row => row.Name == "BatchName")).ShouldBeTrue();
            (await context.Set<StorageDocumentDimensionName>().AnyAsync(row => row.Name == "NotInCommittedPrefix" || row.Name == "NotReached")).ShouldBeFalse();
        }

        // Public writes accept more than ten thousand distinct names in one scope without a dictionary quota.
        var writes = new List<DocumentWrite<Receipt>>();
        for (var owner = 0; owner < 160; owner++)
        {
            var scalars = Enumerable.Range(0, 64).Select(index => ($"Cardinality{owner * 64 + index:D5}", (object)index)).ToArray();
            writes.Add(new(new("cardinality", owner.ToString("D4")), new() { Amount = owner }, new() { Dimensions = DocumentValues(scalars) }));
            using var content = new MemoryStream([7]);
            var written = await blobs.UploadAsync(new() { Key = new("cardinality", owner.ToString("D4")), Content = content, Dimensions = BlobValues(scalars) });
            written.IsSuccess.ShouldBeTrue(Errors(written));
        }

        var inserted = await documents.UpsertManyAsync(writes);
        inserted.IsSuccess.ShouldBeTrue(Errors(inserted));
        inserted.Value.FailedKey.ShouldBeNull();
        inserted.Value.Items.Count.ShouldBe(160);
        await using (var scope = services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MutationContext>();
            (await context.Set<StorageBlobDimensionName>().CountAsync(row => row.Scope == "cardinality")).ShouldBe(10240);
            (await context.Set<StorageDocumentDimensionName>().CountAsync(row => row.Name.StartsWith("Cardinality"))).ShouldBe(10240);
            (await context.Set<StorageBlobDimension>().CountAsync(row => row.Owner.Container == "cardinality")).ShouldBe(10240);
            (await context.Set<StorageDocumentDimension>().CountAsync(row => row.Owner.PartitionKey == "cardinality")).ShouldBe(10240);
        }
    }
}
