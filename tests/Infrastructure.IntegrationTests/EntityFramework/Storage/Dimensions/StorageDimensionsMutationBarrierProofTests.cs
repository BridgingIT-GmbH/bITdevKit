// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Application.Storage;
using Infrastructure.EntityFramework;
using Infrastructure.EntityFramework.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;

public sealed partial class StorageDimensionsMutationProofTests
{
    private static async Task VerifyBarrierRacesAsync(ServiceProvider services, MutationCommands commands)
    {
        var scopes = services.GetRequiredService<IServiceScopeFactory>();
        var blobs = Enumerable.Range(0, 2).Select(_ => new BlobStoreClient("ef", new EntityFrameworkBlobStoreProvider<MutationContext>(scopes))).ToArray();
        var documents = Enumerable.Range(0, 2).Select(_ => new DocumentStoreClient<Receipt>(new EntityFrameworkDocumentStoreProvider<MutationContext>(scopes))).ToArray();
        var blobKey = new BlobKey("barrier", "owner");
        var documentKey = new DocumentKey("barrier", "owner");
        using var content = new MemoryStream([1, 2, 3]);
        var originalBlob = await blobs[0].UploadAsync(new() { Key = blobKey, Content = content, Dimensions = BlobValues(("BarrierValue", 0)) });
        originalBlob.IsSuccess.ShouldBeTrue(Errors(originalBlob));
        var originalDocument = await documents[0].UpsertAsync(documentKey, new() { Amount = 1 }, new() { Dimensions = DocumentValues(("BarrierValue", 0)) });
        originalDocument.IsSuccess.ShouldBeTrue(Errors(originalDocument));

        // Both callers have read the supplied version before either lease/publication update executes.
        commands.Barrier = new("__Storage_Blobs", missingNames: false);
        var blobResults = await Task.WhenAll(Enumerable.Range(0, 2).Select(index => blobs[index].UpdateDimensionsAsync(new()
        {
            Key = blobKey, IfMatchETag = originalBlob.Value.ETag, Dimensions = BlobValues(("BarrierValue", index + 1))
        })));
        commands.Barrier.Arrivals.ShouldBe(2);
        commands.Barrier = null;
        blobResults.Count(result => result.IsSuccess).ShouldBe(1);
        blobResults.Single(result => result.IsFailure).HasError<BlobStoreConflictError>().ShouldBeTrue();
        var winningBlob = blobResults.Single(result => result.IsSuccess).Value;
        var finalBlob = await blobs[0].GetPropertiesAsync(blobKey);
        finalBlob.Value.ETag.ShouldBe(winningBlob.ETag);
        finalBlob.Value.Dimensions["BarrierValue"].Value.ShouldBe(winningBlob.Dimensions["BarrierValue"].Value);

        commands.Barrier = new("__Storage_Documents", missingNames: false);
        var documentResults = await Task.WhenAll(Enumerable.Range(0, 2).Select(index => documents[index].UpdateDimensionsAsync(new()
        {
            Key = documentKey, IfMatchETag = originalDocument.Value.ETag, Dimensions = DocumentValues(("BarrierValue", index + 1))
        })));
        commands.Barrier.Arrivals.ShouldBe(2);
        commands.Barrier = null;
        documentResults.Count(result => result.IsSuccess).ShouldBe(1);
        documentResults.Single(result => result.IsFailure).HasError<DocumentStoreConflictError>().ShouldBeTrue();
        var winningDocument = documentResults.Single(result => result.IsSuccess).Value;
        var finalDocument = await documents[0].GetAsync(documentKey);
        finalDocument.Value.ETag.ShouldBe(winningDocument.ETag);
        finalDocument.Value.Dimensions["BarrierValue"].Value.ShouldBe(winningDocument.Dimensions["BarrierValue"].Value);

        // Both independent resolvers observe a missing dictionary identity before either registers it.
        var raceBlobKeys = Enumerable.Range(0, 2).Select(index => new BlobKey("barrier-register", index.ToString())).ToArray();
        var raceDocumentKeys = Enumerable.Range(0, 2).Select(index => new DocumentKey("barrier-register", index.ToString())).ToArray();
        for (var index = 0; index < 2; index++)
        {
            using var bytes = new MemoryStream([1]);
            (await blobs[index].UploadAsync(new() { Key = raceBlobKeys[index], Content = bytes })).IsSuccess.ShouldBeTrue();
            (await documents[index].UpsertAsync(raceDocumentKeys[index], new() { Amount = 1 })).IsSuccess.ShouldBeTrue();
        }

        commands.Barrier = new("__Storage_BlobDimensionNames", missingNames: true);
        var registeredBlobs = await Task.WhenAll(Enumerable.Range(0, 2).Select(index => blobs[index].UpdateDimensionsAsync(new()
        {
            Key = raceBlobKeys[index], Dimensions = BlobValues(("BarrierRegistered", index))
        })));
        commands.Barrier.Arrivals.ShouldBe(2);
        commands.Barrier = null;
        registeredBlobs.ShouldAllBe(result => result.IsSuccess);

        commands.Barrier = new("__Storage_DocumentDimensionNames", missingNames: true);
        var registeredDocuments = await Task.WhenAll(Enumerable.Range(0, 2).Select(index => documents[index].UpdateDimensionsAsync(new()
        {
            Key = raceDocumentKeys[index], Dimensions = DocumentValues(("BarrierRegistered", index))
        })));
        commands.Barrier.Arrivals.ShouldBe(2);
        commands.Barrier = null;
        registeredDocuments.ShouldAllBe(result => result.IsSuccess);

        await using (var scope = services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MutationContext>();
            (await context.Set<StorageBlobDimensionName>().CountAsync(row => row.Scope == "barrier-register" && row.Name == "BarrierRegistered")).ShouldBe(1);
            (await context.Set<StorageDocumentDimensionName>().CountAsync(row => row.Name == "BarrierRegistered")).ShouldBe(1);
        }

        foreach (var key in raceBlobKeys.Append(blobKey)) (await blobs[0].DeleteAsync(key)).IsSuccess.ShouldBeTrue();
        foreach (var key in raceDocumentKeys.Append(documentKey)) (await documents[0].DeleteAsync(key)).IsSuccess.ShouldBeTrue();
    }

    private sealed class MutationBarrier(string table, bool missingNames)
    {
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrivals;

        public int Arrivals => Volatile.Read(ref this.arrivals);

        public Task BeforeAsync(DbCommand command, CancellationToken cancellationToken) => !missingNames
            && command.CommandText.Contains(table, StringComparison.Ordinal) && command.CommandText.Contains("UPDATE", StringComparison.OrdinalIgnoreCase)
                ? this.EnterAsync(cancellationToken) : Task.CompletedTask;

        public Task AfterAsync(DbCommand command, DbDataReader reader, CancellationToken cancellationToken) => missingNames
            && command.CommandText.Contains(table, StringComparison.Ordinal) && command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) && !reader.HasRows
                ? this.EnterAsync(cancellationToken) : Task.CompletedTask;

        private async Task EnterAsync(CancellationToken cancellationToken)
        {
            if (this.Arrivals >= 2) return;
            if (Interlocked.Increment(ref this.arrivals) == 2) this.release.SetResult();
            await this.release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        }
    }
}
