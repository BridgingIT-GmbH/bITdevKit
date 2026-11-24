// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Application.Storage;
using Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;

public sealed partial class StorageDimensionsMutationProofTests
{
    private static async Task VerifyCommitAndCancellationAsync(ServiceProvider services, BlobStoreClient blobs, DocumentStoreClient<Receipt> documents,
        LostDimensionCommit commits, MutationCommands commands)
    {
        var blobKey = new BlobKey("archive", "receipt");
        var docKey = new DocumentKey("customer", "receipt");
        commits.FailAt = commits.Commits + 1;
        var blobFailed = await blobs.UpdateDimensionsAsync(new() { Key = blobKey, Dimensions = BlobValues(("LostBlob", true)) });
        blobFailed.HasError<BlobStoreIndeterminateOperationError>().ShouldBeTrue();
        commits.Commits.ShouldBe(commits.FailAt);
        (await blobs.GetPropertiesAsync(blobKey)).Value.Dimensions["LostBlob"].Value.ShouldBe(true);
        commits.FailAt = commits.Commits + 1;
        var docFailed = await documents.UpdateDimensionsAsync(new() { Key = docKey, Dimensions = DocumentValues(("LostDoc", true)) });
        docFailed.HasError<DocumentStoreIndeterminateOperationError>().ShouldBeTrue();
        commits.Commits.ShouldBe(commits.FailAt);
        (await documents.GetAsync(docKey)).Value.Dimensions["LostDoc"].Value.ShouldBe(true);

        using (var cancellation = new CancellationTokenSource())
        {
            commands.CancelOnPublication = cancellation;
            await Should.ThrowAsync<OperationCanceledException>(() => blobs.UpdateDimensionsAsync(new() { Key = blobKey,
                Dimensions = BlobValues(("CancelledBlob", true)) }, cancellation.Token));
        }

        using (var cancellation = new CancellationTokenSource())
        {
            commands.CancelOnPublication = cancellation;
            await Should.ThrowAsync<OperationCanceledException>(() => documents.UpdateDimensionsAsync(new() { Key = docKey,
                Dimensions = DocumentValues(("CancelledDoc", true)) }, cancellation.Token));
        }

        commands.CancelOnPublication = null;
        await using (var scope = services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MutationContext>();
            (await context.Set<StorageBlobDimensionName>().AnyAsync(row => row.Name == "CancelledBlob")).ShouldBeFalse();
            (await context.Set<StorageDocumentDimensionName>().AnyAsync(row => row.Name == "CancelledDoc")).ShouldBeFalse();
        }

        (await blobs.GetPropertiesAsync(blobKey)).Value.Dimensions["LostBlob"].Value.ShouldBe(true);
        (await documents.GetAsync(docKey)).Value.Dimensions["LostDoc"].Value.ShouldBe(true);
        (await blobs.UpdateDimensionsAsync(new() { Key = blobKey, Dimensions = BlobValues(("CancelledBlob", true)) })).IsSuccess.ShouldBeTrue();
        (await documents.UpdateDimensionsAsync(new() { Key = docKey, Dimensions = DocumentValues(("CancelledDoc", true)) })).IsSuccess.ShouldBeTrue();
    }

    private sealed class LostDimensionCommit : DbTransactionInterceptor
    {
        public int Commits { get; private set; }

        public int FailAt { get; set; } = -1;

        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            this.Commits++;
            if (this.Commits == this.FailAt) throw new LostDimensionAcknowledgement();
            return Task.CompletedTask;
        }
    }

    private sealed class LostDimensionAcknowledgement : DbException;
}
