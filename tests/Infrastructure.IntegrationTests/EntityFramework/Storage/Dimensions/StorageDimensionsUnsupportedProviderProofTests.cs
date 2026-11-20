// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Application.Storage;
using Infrastructure.Azure;
using Infrastructure.FileSystem;
using global::Azure.Storage.Blobs;
using global::Azure.Data.Tables;

public sealed class StorageDimensionsUnsupportedProviderProofTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnsupportedProviders_ExplicitCollectionsAndFilters_FailBeforeBackendOrStreamAccess(bool populated)
    {
        var blobService = Substitute.For<BlobServiceClient>();
        var tableService = Substitute.For<TableServiceClient>();
        var cosmos = Substitute.For<ICosmosSqlProvider<CosmosStorageDocument>>();
        var path = Path.Combine(Path.GetTempPath(), $"bdk-never-create-{Guid.NewGuid():N}");
        var options = new FileSystemStorageOptions { RootPath = path, CreateRoot = false };
        IBlobStoreProvider[] blobs = [new InMemoryBlobStoreProvider(), new AzureBlobStoreProvider(blobService), new FileSystemBlobStoreProvider(options)];
        IDocumentStoreProvider[] documents = [new InMemoryDocumentStoreProvider(), new AzureBlobDocumentStoreProvider(null, blobService), new AzureTableDocumentStoreProvider(null, tableService), new CosmosDocumentStoreProvider(cosmos), new FileSystemDocumentStoreProvider(options)];
        var blobDimensions = populated ? BlobDimensions.Create(new Dictionary<string, object> { ["year"] = 2026 }).Value : BlobDimensions.Empty;
        var documentDimensions = populated ? DocumentDimensions.Create(new Dictionary<string, object> { ["year"] = 2026 }).Value : DocumentDimensions.Empty;
        var blobFilter = BlobDimensionFilter.Where(BlobDimensionPredicate.Create("year", BlobDimensionOperator.Equal, 2026).Value);
        var documentFilter = DocumentDimensionFilter.Where(DocumentDimensionPredicate.Create("year", DocumentDimensionOperator.Equal, 2026).Value);
        blobService.ClearReceivedCalls();
        tableService.ClearReceivedCalls();
        foreach (var provider in blobs)
        {
            using var content = new NeverRead();
            var key = new BlobKey("archive", "owner");
            (await provider.UploadAsync(new() { Key = key, Content = content, Dimensions = blobDimensions })).HasError<BlobDimensionsNotSupportedError>().ShouldBeTrue();
            (await provider.UpdateDimensionsAsync(new() { Key = key, Dimensions = blobDimensions })).HasError<BlobDimensionsNotSupportedError>().ShouldBeTrue();
            (await provider.ListPageAsync(new() { Container = "archive", DimensionFilter = blobFilter })).HasError<BlobDimensionsNotSupportedError>().ShouldBeTrue();
            var client = new BlobStoreClient("unsupported", provider);
            (await client.UploadAsync(new() { Key = key, Content = content, Dimensions = blobDimensions })).HasError<BlobDimensionsNotSupportedError>().ShouldBeTrue();
            content.Reads.ShouldBe(0);
        }

        foreach (var provider in documents)
        {
            var type = DocumentTypeIdentity.For<Invoice>();
            var key = new DocumentKey("archive", "owner");
            (await provider.UpsertAsync(type, new() { Key = key, Dimensions = documentDimensions })).HasError<DocumentDimensionsNotSupportedError>().ShouldBeTrue();
            (await provider.UpdateDimensionsAsync(type, new() { Key = key, Dimensions = documentDimensions })).HasError<DocumentDimensionsNotSupportedError>().ShouldBeTrue();
            (await provider.FindPageAsync(type, new() { DimensionFilter = documentFilter }, DateTimeOffset.UtcNow)).HasError<DocumentDimensionsNotSupportedError>().ShouldBeTrue();
            (await provider.ListPageAsync(type, new() { DimensionFilter = documentFilter }, DateTimeOffset.UtcNow)).HasError<DocumentDimensionsNotSupportedError>().ShouldBeTrue();
            (await provider.CountAsync(type, new() { DimensionFilter = documentFilter }, DateTimeOffset.UtcNow)).HasError<DocumentDimensionsNotSupportedError>().ShouldBeTrue();
            var client = new DocumentStoreClient<Invoice>(provider);
            (await client.UpsertAsync(key, new(), new() { Dimensions = documentDimensions })).HasError<DocumentDimensionsNotSupportedError>().ShouldBeTrue();
        }

        blobService.ReceivedCalls().ShouldBeEmpty();
        tableService.ReceivedCalls().ShouldBeEmpty();
        cosmos.ReceivedCalls().ShouldBeEmpty();
        Directory.Exists(path).ShouldBeFalse();
    }

    public sealed class Invoice { }

    private sealed class NeverRead : Stream
    {
        public int Reads { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) { this.Reads++; throw new InvalidOperationException("Input must not be consumed."); }

        public override void Flush() => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
