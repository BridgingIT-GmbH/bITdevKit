// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Application.Storage;
using Infrastructure.EntityFramework;
using Infrastructure.EntityFramework.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public sealed partial class StorageDimensionsMutationProofTests
{
    private static async Task VerifyConditionsAndRetentionAsync(ServiceProvider services, BlobStoreClient blobs, DocumentStoreClient<Receipt> documents, MutationCommands commands)
    {
        var blobKey = new BlobKey("archive", "receipt");
        var docKey = new DocumentKey("customer", "receipt");
        var blobBefore = (await blobs.GetPropertiesAsync(blobKey)).Value;
        var docBefore = (await documents.GetAsync(docKey)).Value;
        commands.Text.Clear();
        var blobProperties = await blobs.UpdatePropertiesAsync(new() { Key = blobKey, IfMatchETag = blobBefore.ETag, Properties = new() { ["reviewed"] = true } });
        blobProperties.IsSuccess.ShouldBeTrue();
        var docProperties = await documents.UpdatePropertiesAsync(new(docKey) { IfMatchETag = docBefore.ETag, Properties = new() { ["reviewed"] = true } });
        docProperties.IsSuccess.ShouldBeTrue();
        commands.Text.ShouldNotContain(text => text.Contains("__Storage_BlobDimension") || text.Contains("__Storage_DocumentDimension"));
        (await blobs.UpdateDimensionsAsync(new() { Key = blobKey, IfMatchETag = blobBefore.ETag, Dimensions = BlobValues(("NeverRegisterStale", true)) })).HasError<BlobStoreConflictError>().ShouldBeTrue();
        (await documents.UpdateDimensionsAsync(new() { Key = docKey, IfMatchETag = docBefore.ETag, Dimensions = DocumentValues(("NeverRegisterStale", true)) })).HasError<DocumentStoreConflictError>().ShouldBeTrue();
        var blobDimensions = await blobs.UpdateDimensionsAsync(new() { Key = blobKey, IfMatchETag = blobProperties.Value.ETag, Dimensions = BlobValues(("Conditional", true)) });
        var docDimensions = await documents.UpdateDimensionsAsync(new() { Key = docKey, IfMatchETag = docProperties.Value.ETag, Dimensions = DocumentValues(("Conditional", true)) });
        blobDimensions.IsSuccess.ShouldBeTrue();
        docDimensions.IsSuccess.ShouldBeTrue();
        using var staleContent = new MemoryStream([8, 9]);
        (await blobs.UploadAsync(new() { Key = blobKey, Content = staleContent, IfMatchETag = blobProperties.Value.ETag })).HasError<BlobStoreConflictError>().ShouldBeTrue();
        staleContent.Position.ShouldBe(0);
        (await documents.UpsertAsync(docKey, new() { Amount = 99 }, new() { IfMatchETag = docProperties.Value.ETag })).HasError<DocumentStoreConflictError>().ShouldBeTrue();
        (await blobs.DeleteAsync(blobKey, new() { IfMatchETag = blobProperties.Value.ETag })).HasError<BlobStoreConflictError>().ShouldBeTrue();
        (await documents.DeleteAsync(docKey, new() { IfMatchETag = docProperties.Value.ETag })).HasError<DocumentStoreConflictError>().ShouldBeTrue();
        (await blobs.UpdateDimensionsAsync(new() { Key = new("missing", "owner"), Dimensions = BlobValues(("NeverRegisterMissing", true)) })).HasError<BlobStoreNotFoundError>().ShouldBeTrue();
        (await documents.UpdateDimensionsAsync(new() { Key = new("missing", "owner"), Dimensions = DocumentValues(("NeverRegisterMissing", true)) })).HasError<DocumentStoreNotFoundError>().ShouldBeTrue();
        var commandsBeforeInvalid = commands.Text.Count;
        using var invalidContent = new MemoryStream([1]);
        (await blobs.UploadAsync(new() { Key = blobKey, Content = invalidContent, IfMatchETag = blobDimensions.Value.ETag, OverwriteMode = BlobOverwriteMode.FailIfExists,
            Dimensions = BlobValues(("NeverRegisterInvalid", true)) })).IsFailure.ShouldBeTrue();
        (await documents.UpsertAsync(docKey, new() { Amount = 99 }, new() { IfMatchETag = docDimensions.Value.ETag, CreateOnly = true,
            Dimensions = DocumentValues(("NeverRegisterInvalid", true)) })).IsFailure.ShouldBeTrue();
        invalidContent.Position.ShouldBe(0);
        commands.Text.Count.ShouldBe(commandsBeforeInvalid);

        var expiry = DateTimeOffset.UtcNow.AddMinutes(1);
        using var expiringContent = new MemoryStream([1]);
        (await blobs.UploadAsync(new() { Key = new("retention", "owner"), Content = expiringContent, ExpiresAt = expiry,
            Dimensions = BlobValues(("RetainedBySweep", true)) })).IsSuccess.ShouldBeTrue();
        (await documents.UpsertAsync(new("retention", "owner"), new() { Amount = 1 }, new() { Expiration = ExpirationChange.At(expiry),
            Dimensions = DocumentValues(("RetainedBySweep", true)) })).IsSuccess.ShouldBeTrue();
        var scopes = services.GetRequiredService<IServiceScopeFactory>();
        var blobSweep = await new EntityFrameworkBlobStoreProvider<MutationContext>(scopes).SweepExpiredAsync(new() { ExpiresOnOrBefore = expiry.AddSeconds(1), MaxBatches = 2, BatchSize = 2 });
        var docSweep = await new EntityFrameworkDocumentStoreProvider<MutationContext>(scopes).SweepExpiredAsync(new() { DocumentType = DocumentTypeIdentity.For<Receipt>(), VisibilityCutoff = expiry.AddSeconds(1), MaxBatches = 2, BatchSize = 2 });
        blobSweep.IsSuccess.ShouldBeTrue(Errors(blobSweep));
        docSweep.IsSuccess.ShouldBeTrue(Errors(docSweep));
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MutationContext>();
        var blobName = await context.Set<StorageBlobDimensionName>().SingleAsync(row => row.Name == "RetainedBySweep");
        var docName = await context.Set<StorageDocumentDimensionName>().SingleAsync(row => row.Name == "RetainedBySweep");
        (await context.Set<StorageBlobDimension>().CountAsync(row => row.NameId == blobName.Id)).ShouldBe(0);
        (await context.Set<StorageDocumentDimension>().CountAsync(row => row.NameId == docName.Id)).ShouldBe(0);
        (await context.Set<StorageBlobDimensionName>().AnyAsync(row => row.Name.StartsWith("NeverRegister"))).ShouldBeFalse();
        (await context.Set<StorageDocumentDimensionName>().AnyAsync(row => row.Name.StartsWith("NeverRegister"))).ShouldBeFalse();
    }
}
