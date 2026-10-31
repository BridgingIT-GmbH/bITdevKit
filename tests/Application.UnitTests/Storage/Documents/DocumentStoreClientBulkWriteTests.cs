// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;

[UnitTest("Application")]
public sealed class DocumentStoreClientBulkWriteTests
{
    [Fact]
    public async Task UpsertManyAsync_WithBulkProvider_UsesOneBulkCallAndSetsContentLength()
    {
        var provider = Substitute.For<IDocumentStoreProvider, IDocumentStoreBulkWriteProvider>();
        var bulk = (IDocumentStoreBulkWriteProvider)provider;
        bulk.UpsertManyAsync(default, default, default).ReturnsForAnyArgs(call =>
        {
            var writes = call.Arg<IReadOnlyList<StoredDocumentWrite>>();
            return Result<DocumentBatchResult<DocumentInfo>>.Success(new()
            {
                Items = writes.Select(x => new DocumentInfo { Key = x.Key }).ToArray()
            });
        });
        var sut = new DocumentStoreClient<BulkDocument>(provider);

        var result = await sut.UpsertManyAsync([
            new(new("p", "a"), new() { Value = "A" }),
            new(new("p", "b"), new() { Value = "BB" })]);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Select(x => x.Key.RowKey).ShouldBe(["a", "b"]);
        result.Value.Items.ShouldAllBe(x => x.ContentLength > 0);
        await bulk.Received(1).UpsertManyAsync(
            Arg.Any<DocumentTypeIdentity>(),
            Arg.Is<IReadOnlyList<StoredDocumentWrite>>(x => x.Count == 2),
            Arg.Any<CancellationToken>());
        await provider.DidNotReceiveWithAnyArgs().UpsertAsync(default, default, default);
    }

    [Fact]
    public async Task UpsertManyAsync_WhenBulkProviderStopsAtFailedKey_ReportsFailedKeyAfterPrefix()
    {
        var provider = Substitute.For<IDocumentStoreProvider, IDocumentStoreBulkWriteProvider>();
        ((IDocumentStoreBulkWriteProvider)provider).UpsertManyAsync(default, default, default).ReturnsForAnyArgs(
            Result<DocumentBatchResult<DocumentInfo>>.Success(new()
            {
                Items = [new DocumentInfo { Key = new("p", "a") }],
                FailedKey = new("p", "b"),
                FailedKeys = [new("p", "b")]
            }).WithMessages(["conflict"]));
        var sut = new DocumentStoreClient<BulkDocument>(provider);

        var result = await sut.UpsertManyAsync([
            new(new("p", "a"), new() { Value = "A" }),
            new(new("p", "b"), new() { Value = "B" }),
            new(new("p", "c"), new() { Value = "C" })]);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(1);
        result.Value.FailedKey.ShouldBe(new DocumentKey("p", "b"));
        result.Messages.ShouldContain("conflict");
    }

    [Fact]
    public async Task UpsertManyAsync_WithOversizedDocumentAfterValidPrefix_SendsPrefixAndReportsFailedKey()
    {
        var provider = Substitute.For<IDocumentStoreProvider, IDocumentStoreBulkWriteProvider>();
        var bulk = (IDocumentStoreBulkWriteProvider)provider;
        bulk.UpsertManyAsync(default, default, default).ReturnsForAnyArgs(call =>
            Result<DocumentBatchResult<DocumentInfo>>.Success(new()
            {
                Items = call.Arg<IReadOnlyList<StoredDocumentWrite>>().Select(x => new DocumentInfo { Key = x.Key }).ToArray()
            }));
        var sut = new DocumentStoreClient<BulkDocument>(provider, options: new DocumentStoreOptions { MaxDocumentSize = 64 });

        var result = await sut.UpsertManyAsync([
            new(new("p", "a"), new() { Value = "A" }),
            new(new("p", "big"), new() { Value = new string('x', 500) }),
            new(new("p", "c"), new() { Value = "C" })]);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Select(x => x.Key.RowKey).ShouldBe(["a"]);
        result.Value.FailedKey.ShouldBe(new DocumentKey("p", "big"));
        await bulk.Received(1).UpsertManyAsync(
            Arg.Any<DocumentTypeIdentity>(),
            Arg.Is<IReadOnlyList<StoredDocumentWrite>>(x => x.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpsertManyAsync_WhenFirstDocumentIsOversized_DoesNotCallProvider()
    {
        var provider = Substitute.For<IDocumentStoreProvider, IDocumentStoreBulkWriteProvider>();
        var sut = new DocumentStoreClient<BulkDocument>(provider, options: new DocumentStoreOptions { MaxDocumentSize = 64 });

        var result = await sut.UpsertManyAsync([new(new("p", "big"), new() { Value = new string('x', 500) })]);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldBeEmpty();
        result.Value.FailedKey.ShouldBe(new DocumentKey("p", "big"));
        await ((IDocumentStoreBulkWriteProvider)provider).DidNotReceiveWithAnyArgs().UpsertManyAsync(default, default, default);
    }

    [Fact]
    public async Task UpsertManyAsync_WhenBulkProviderFails_ReturnsOuterFailure()
    {
        var provider = Substitute.For<IDocumentStoreProvider, IDocumentStoreBulkWriteProvider>();
        ((IDocumentStoreBulkWriteProvider)provider).UpsertManyAsync(default, default, default)
            .ReturnsForAnyArgs(Result<DocumentBatchResult<DocumentInfo>>.Failure(new DocumentStoreProviderError("down")));
        var sut = new DocumentStoreClient<BulkDocument>(provider);

        var result = await sut.UpsertManyAsync([new(new("p", "a"), new() { Value = "A" })]);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteManyAsync_WithBulkProvider_UsesOneBulkCall()
    {
        var provider = Substitute.For<IDocumentStoreProvider, IDocumentStoreBulkWriteProvider>();
        var bulk = (IDocumentStoreBulkWriteProvider)provider;
        bulk.DeleteManyAsync(default, default, default).ReturnsForAnyArgs(
            Result<DocumentBatchResult<DocumentKey>>.Success(new() { Items = [new("p", "a"), new("p", "b")] }));
        var sut = new DocumentStoreClient<BulkDocument>(provider);

        var result = await sut.DeleteManyAsync([new(new("p", "a")), new(new("p", "b"))]);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(2);
        await bulk.Received(1).DeleteManyAsync(
            Arg.Any<DocumentTypeIdentity>(),
            Arg.Is<IReadOnlyList<DocumentDelete>>(x => x.Count == 2),
            Arg.Any<CancellationToken>());
        await provider.DidNotReceiveWithAnyArgs().DeleteAsync(default, default, default, default);
    }

    [Fact]
    public async Task DeleteManyAsync_WithEmptyList_ReturnsEmptySuccessWithoutProviderCall()
    {
        var provider = Substitute.For<IDocumentStoreProvider, IDocumentStoreBulkWriteProvider>();
        var sut = new DocumentStoreClient<BulkDocument>(provider);

        var result = await sut.DeleteManyAsync([]);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldBeEmpty();
        await ((IDocumentStoreBulkWriteProvider)provider).DidNotReceiveWithAnyArgs().DeleteManyAsync(default, default, default);
    }

    [Fact]
    public async Task UpsertManyAsync_WithoutBulkProvider_FallsBackToSequentialWrites()
    {
        var sut = new DocumentStoreClient<BulkDocument>(new InMemoryDocumentStoreProvider());

        var result = await sut.UpsertManyAsync([
            new(new("p", "a"), new() { Value = "A" }),
            new(new("p", "b"), new() { Value = "B" })]);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(2);
        (await sut.GetAsync(new("p", "b"))).IsSuccess.ShouldBeTrue();
    }

    public sealed class BulkDocument { public string Value { get; set; } }
}
