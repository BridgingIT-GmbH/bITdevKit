// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.Azure.Storage;

using Application.Storage;
using global::Azure;
using global::Azure.Core;
using global::Azure.Data.Tables;
using global::Azure.Data.Tables.Models;
using Infrastructure.Azure;

/// <summary>Exercises Table document mutation conditions without an external storage service.</summary>
public class AzureTableDocumentStoreConcurrencyTests
{
    [Fact]
    public async Task UpsertAsync_MissingConditionalTarget_ReturnsConflictWithoutInsert()
    {
        // Arrange
        var table = Substitute.For<TableClient>();
        table.GetEntityAsync<TableEntity>(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<Response<TableEntity>>(new RequestFailedException(404, "Missing entity")));
        var sut = CreateProvider(table);

        // Act
        var result = await sut.UpsertAsync(DocumentTypeIdentity.For<Document>(), Write("old-version"));

        // Assert
        result.Errors.ShouldContain(error => error is DocumentStoreConflictError);
        await table.DidNotReceive().AddEntityAsync(Arg.Any<TableEntity>(), Arg.Any<CancellationToken>());
        await table.DidNotReceive().UpdateEntityAsync(Arg.Any<TableEntity>(), Arg.Any<ETag>(), Arg.Any<TableUpdateMode>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpsertAsync_PhysicalReadFails_ReturnsProviderFailureWithoutInsert()
    {
        // Arrange
        var table = Substitute.For<TableClient>();
        table.GetEntityAsync<TableEntity>(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<Response<TableEntity>>(new RequestFailedException(503, "Storage unavailable")));
        var sut = CreateProvider(table);

        // Act
        var result = await sut.UpsertAsync(DocumentTypeIdentity.For<Document>(), Write());

        // Assert
        result.Errors.ShouldContain(error => error is DocumentStoreProviderError);
        await table.DidNotReceive().AddEntityAsync(Arg.Any<TableEntity>(), Arg.Any<CancellationToken>());
        await table.DidNotReceive().UpdateEntityAsync(Arg.Any<TableEntity>(), Arg.Any<ETag>(), Arg.Any<TableUpdateMode>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpsertAsync_AnotherWriteFollowsCommit_ReturnsOwnCommittedMetadata(bool existing)
    {
        // Arrange
        var table = Substitute.For<TableClient>();
        var reads = 0;
        table.GetEntityAsync<TableEntity>(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                reads++;
                if (reads == 1 && !existing)
                {
                    return Task.FromException<Response<TableEntity>>(new RequestFailedException(404, "Missing entity"));
                }

                return Task.FromResult(Response.FromValue(Entity(reads == 1 ? "original-version" : "later-version"), new TestResponse()));
            });
        table.AddEntityAsync(Arg.Any<TableEntity>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Response>(new TestResponse("committed-version")));
        table.UpdateEntityAsync(Arg.Any<TableEntity>(), Arg.Any<ETag>(), Arg.Any<TableUpdateMode>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Response>(new TestResponse("committed-version")));
        var sut = CreateProvider(table);

        // Act
        var result = await sut.UpsertAsync(DocumentTypeIdentity.For<Document>(), Write());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ETag.ShouldBe("committed-version");
        result.Value.ContentHash.ShouldBe("written-content-hash");
        reads.ShouldBe(1);
    }

    [Fact]
    public async Task UpdatePropertiesAsync_ReturnedVersion_ProtectsFollowingContentWrite()
    {
        // Arrange
        var table = Substitute.For<TableClient>();
        var storedEtag = "version-1";
        var updates = 0;
        table.GetEntityAsync<TableEntity>(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Response.FromValue(Entity(storedEtag), new TestResponse())));
        table.UpdateEntityAsync(Arg.Any<TableEntity>(), Arg.Any<ETag>(), TableUpdateMode.Replace, Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                if (call.Arg<ETag>().ToString() != storedEtag)
                {
                    return Task.FromException<Response>(new RequestFailedException(412, "The version changed"));
                }

                storedEtag = $"version-{++updates + 1}";
                return Task.FromResult<Response>(new TestResponse(storedEtag));
            });
        var sut = CreateProvider(table);
        var type = DocumentTypeIdentity.For<Document>();

        // Act
        var metadata = await sut.UpdatePropertiesAsync(type, new(new("records", "42")) { IfMatchETag = storedEtag }, null, true);
        metadata.IsSuccess.ShouldBeTrue();
        var content = await sut.UpsertAsync(type, Write(metadata.Value.ETag));

        // Assert
        metadata.Value.ETag.ShouldBe("version-2");
        content.IsSuccess.ShouldBeTrue();
        content.Value.ETag.ShouldBe("version-3");
        await table.Received(1).UpdateEntityAsync(Arg.Any<TableEntity>(), new ETag("version-2"), TableUpdateMode.Replace, Arg.Any<CancellationToken>());
    }

    private static AzureTableDocumentStoreProvider CreateProvider(TableClient table)
    {
        table.CreateIfNotExistsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Response.FromValue<TableItem>(null, new TestResponse())));
        var service = Substitute.For<TableServiceClient>();
        service.GetTableClient(Arg.Any<string>()).Returns(table);
        return new(null, service);
    }

    private static StoredDocumentWrite Write(string etag = null) => new()
    {
        Key = new("records", "42"),
        Content = [1, 2, 3],
        ContentHash = "written-content-hash",
        StoredContentHash = "written-content-hash",
        Options = new() { IfMatchETag = etag }
    };

    private static TableEntity Entity(string etag) => new("cmVjb3Jkcw", "NDI")
    {
        ETag = new ETag(etag),
        ["bdk_partition_key"] = "records",
        ["bdk_row_key"] = "42",
        ["bdk_content_hash"] = "original-content-hash",
        ["bdk_created_at"] = DateTimeOffset.UnixEpoch,
        ["bdk_modified_at"] = DateTimeOffset.UnixEpoch,
        ["bdk_content_chunk_count"] = 1,
        ["bdk_content_0000"] = new byte[] { 4, 5, 6 }
    };

    private sealed class Document;

    private sealed class TestResponse(string etag = null) : Response
    {
        public override int Status => 204;

        public override string ReasonPhrase => "No Content";

        public override Stream ContentStream { get; set; }

        public override string ClientRequestId { get; set; }

        public override void Dispose() { }

        protected override bool ContainsHeader(string name) => etag is not null && string.Equals(name, "ETag", StringComparison.OrdinalIgnoreCase);

        protected override IEnumerable<HttpHeader> EnumerateHeaders() => etag is null ? [] : [new("ETag", etag)];

        protected override bool TryGetHeader(string name, out string value)
        {
            value = this.ContainsHeader(name) ? etag : null;
            return value is not null;
        }

        protected override bool TryGetHeaderValues(string name, out IEnumerable<string> values)
        {
            var found = this.TryGetHeader(name, out var value);
            values = found ? [value] : [];
            return found;
        }
    }
}
