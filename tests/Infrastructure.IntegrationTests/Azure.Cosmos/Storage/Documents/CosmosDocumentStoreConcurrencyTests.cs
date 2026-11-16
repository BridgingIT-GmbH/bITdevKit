// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.Azure.Storage;

using System.Linq.Expressions;
using System.Net;
using Application.Storage;
using Infrastructure.Azure;
using Microsoft.Azure.Cosmos;

public class CosmosDocumentStoreConcurrencyTests
{
    [Theory]
    [InlineData(false, HttpStatusCode.NotFound)]
    [InlineData(true, HttpStatusCode.NotFound)]
    [InlineData(false, HttpStatusCode.PreconditionFailed)]
    [InlineData(true, HttpStatusCode.PreconditionFailed)]
    public async Task UpsertAsync_WhenReplacementLosesObservedVersion_ReturnsConflictWithoutCreating(bool quotedToken, HttpStatusCode status)
    {
        // Arrange
        var current = CreateDocument();
        var provider = CreateProvider(current);
        provider.UpsertItemAsync(Arg.Any<CosmosStorageDocument>(), Arg.Any<object>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<CosmosStorageDocument>(new CosmosException("The observed version no longer exists.", status, 0, "activity", 0)));
        var sut = new CosmosDocumentStoreProvider(provider);

        // Act
        var result = await sut.UpsertAsync(new("contracts"), new()
        {
            Key = new("partition", "row"),
            Content = [2],
            Options = new() { IfMatchETag = quotedToken ? "\"original-version\"" : "original-version" }
        });

        // Assert
        result.HasError<DocumentStoreConflictError>().ShouldBeTrue();
        await provider.Received(1).UpsertItemAsync(Arg.Any<CosmosStorageDocument>(), "default:contracts", "\"original-version\"", Arg.Any<CancellationToken>());
        await provider.DidNotReceive().CreateItemAsync(Arg.Any<CosmosStorageDocument>(), Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false, HttpStatusCode.NotFound)]
    [InlineData(true, HttpStatusCode.NotFound)]
    [InlineData(false, HttpStatusCode.PreconditionFailed)]
    [InlineData(true, HttpStatusCode.PreconditionFailed)]
    public async Task UpdatePropertiesAsync_WhenReplacementLosesObservedVersion_ReturnsConflictWithoutCreating(bool quotedToken, HttpStatusCode status)
    {
        // Arrange
        var provider = CreateProvider(CreateDocument());
        provider.UpsertItemAsync(Arg.Any<CosmosStorageDocument>(), Arg.Any<object>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<CosmosStorageDocument>(new CosmosException("The observed version no longer exists.", status, 0, "activity", 0)));
        var sut = new CosmosDocumentStoreProvider(provider);

        // Act
        var result = await sut.UpdatePropertiesAsync(new("contracts"), new(new("partition", "row"))
        {
            Properties = new() { ["status"] = "approved" },
            IfMatchETag = quotedToken ? "\"original-version\"" : "original-version"
        }, null, true);

        // Assert
        result.HasError<DocumentStoreConflictError>().ShouldBeTrue();
        await provider.Received(1).UpsertItemAsync(Arg.Any<CosmosStorageDocument>(), "default:contracts", "\"original-version\"", Arg.Any<CancellationToken>());
        await provider.DidNotReceive().CreateItemAsync(Arg.Any<CosmosStorageDocument>(), Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ConditionalMutation_WithMatchingVersion_ReturnsCommittedToken(bool metadataOnly, bool quotedServerToken)
    {
        // Arrange
        var current = CreateDocument();
        current.ETag = quotedServerToken ? "\"original-version\"" : "original-version";
        var originalHeader = current.ETag;
        var provider = CreateProvider(current);
        provider.UpsertItemAsync(Arg.Any<CosmosStorageDocument>(), Arg.Any<object>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var saved = call.Arg<CosmosStorageDocument>();
                saved.ETag = "\"committed-version\"";
                return Task.FromResult(saved);
            });
        var sut = new CosmosDocumentStoreProvider(provider);
        var condition = quotedServerToken ? "original-version" : "\"original-version\"";

        // Act
        var result = metadataOnly
            ? await sut.UpdatePropertiesAsync(new("contracts"), new(new("partition", "row")) { IfMatchETag = condition, Properties = new() { ["status"] = "approved" } }, null, true)
            : await sut.UpsertAsync(new("contracts"), new() { Key = new("partition", "row"), Content = [2], Options = new() { IfMatchETag = condition } });

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ETag.ShouldBe("committed-version");
        await provider.Received(1).UpsertItemAsync(Arg.Any<CosmosStorageDocument>(), "default:contracts", originalHeader, Arg.Any<CancellationToken>());
        await provider.DidNotReceive().CreateItemAsync(Arg.Any<CosmosStorageDocument>(), Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    private static ICosmosSqlProvider<CosmosStorageDocument> CreateProvider(CosmosStorageDocument current)
    {
        var provider = Substitute.For<ICosmosSqlProvider<CosmosStorageDocument>>();
        provider.ReadItemsAsync(Arg.Any<Expression<Func<CosmosStorageDocument, bool>>>(), partitionKeyValue: Arg.Any<object>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<CosmosStorageDocument>>([current]));
        return provider;
    }

    private static CosmosStorageDocument CreateDocument() => new()
    {
        Id = "document-id",
        Type = "default:contracts",
        PartitionKey = "partition",
        RowKey = "row",
        Content = [1],
        CreatedDate = DateTimeOffset.UtcNow,
        ETag = "\"original-version\""
    };
}
