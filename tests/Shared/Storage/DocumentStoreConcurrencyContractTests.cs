// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Shared.Storage;

using System.Text;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using Shouldly;
using Xunit;

/// <summary>Defines whole-document optimistic concurrency requirements for raw storage providers.</summary>
public abstract class DocumentStoreConcurrencyContractTests
{
    private static readonly DocumentTypeIdentity DocumentType = DocumentTypeIdentity.For<ContractDocument>();

    /// <summary>Creates an isolated provider using the implementing test project's normal fixture.</summary>
    protected abstract IDocumentStoreProvider CreateProvider();

    [Fact]
    public async Task UpsertAsync_MatchingThenStaleVersion_PreservesCommittedDocument()
    {
        // Arrange
        var sut = this.CreateProvider();
        var key = CreateKey();
        var original = await sut.UpsertAsync(DocumentType, Write(key, "original"));
        original.IsSuccess.ShouldBeTrue();

        // Act
        var replacement = await sut.UpsertAsync(DocumentType, Write(key, "replacement", original.Value.ETag));
        replacement.IsSuccess.ShouldBeTrue();
        var expected = await ReadAsync(sut, key);
        var stale = await sut.UpsertAsync(DocumentType, Write(key, "stale", original.Value.ETag));
        var staleDelete = await sut.DeleteAsync(DocumentType, key, new() { IfMatchETag = original.Value.ETag });

        // Assert
        replacement.Value.ETag.ShouldNotBe(original.Value.ETag);
        expected.ETag.ShouldBe(replacement.Value.ETag);
        expected.Content.ShouldBe(Encoding.UTF8.GetBytes("replacement"));
        stale.Errors.ShouldContain(error => error is DocumentStoreConflictError);
        staleDelete.Errors.ShouldContain(error => error is DocumentStoreConflictError);
        AssertUnchanged(await ReadAsync(sut, key), expected);
    }

    [Fact]
    public async Task UpdatePropertiesAsync_MetadataChanges_InvalidatesEarlierContentVersion()
    {
        // Arrange
        var sut = this.CreateProvider();
        var key = CreateKey();
        var original = await sut.UpsertAsync(DocumentType, Write(key, "original"));
        original.IsSuccess.ShouldBeTrue();
        var properties = Properties("metadata");

        // Act
        var metadata = await sut.UpdatePropertiesAsync(DocumentType,
            new(key) { IfMatchETag = original.Value.ETag, Properties = properties },
            DateTimeOffset.UtcNow.AddDays(2), false);
        metadata.IsSuccess.ShouldBeTrue();
        var expected = await ReadAsync(sut, key);
        var stale = await sut.UpsertAsync(DocumentType, Write(key, "stale", original.Value.ETag));

        // Assert
        metadata.Value.ETag.ShouldNotBe(original.Value.ETag);
        expected.ETag.ShouldBe(metadata.Value.ETag);
        expected.Content.ShouldBe(Encoding.UTF8.GetBytes("original"));
        expected.Properties.Get<string>("label").ShouldBe("metadata");
        stale.Errors.ShouldContain(error => error is DocumentStoreConflictError);
        AssertUnchanged(await ReadAsync(sut, key), expected);

        var next = await sut.UpsertAsync(DocumentType, Write(key, "next", metadata.Value.ETag));
        next.IsSuccess.ShouldBeTrue();
        next.Value.ETag.ShouldNotBe(metadata.Value.ETag);
        (await ReadAsync(sut, key)).ETag.ShouldBe(next.Value.ETag);
    }

    [Fact]
    public async Task UpdatePropertiesAsync_ContentChanges_RejectsEarlierMetadataVersion()
    {
        // Arrange
        var sut = this.CreateProvider();
        var key = CreateKey();
        var original = await sut.UpsertAsync(DocumentType, Write(key, "original"));
        original.IsSuccess.ShouldBeTrue();
        var replacement = await sut.UpsertAsync(DocumentType, Write(key, "replacement", original.Value.ETag));
        replacement.IsSuccess.ShouldBeTrue();
        var expected = await ReadAsync(sut, key);

        // Act
        var stale = await sut.UpdatePropertiesAsync(DocumentType,
            new(key) { IfMatchETag = original.Value.ETag, Properties = Properties("stale") },
            null, false);

        // Assert
        stale.Errors.ShouldContain(error => error is DocumentStoreConflictError);
        AssertUnchanged(await ReadAsync(sut, key), expected);
    }

    [Fact]
    public async Task UpsertAsync_DeletedThenRecreatedKey_RejectsOriginalVersion()
    {
        // Arrange
        var sut = this.CreateProvider();
        var key = CreateKey();
        var original = await sut.UpsertAsync(DocumentType, Write(key, "original"));
        original.IsSuccess.ShouldBeTrue();
        (await sut.DeleteAsync(DocumentType, key, new() { IfMatchETag = original.Value.ETag })).IsSuccess.ShouldBeTrue();

        // Act
        var absent = await sut.UpsertAsync(DocumentType, Write(key, "stale", original.Value.ETag));

        // Assert
        absent.Errors.ShouldContain(error => error is DocumentStoreConflictError);
        (await sut.GetAsync(DocumentType, key, DateTimeOffset.UtcNow)).Errors.ShouldContain(error => error is DocumentStoreNotFoundError);

        var recreated = await sut.UpsertAsync(DocumentType, Write(key, "recreated"));
        recreated.IsSuccess.ShouldBeTrue();
        recreated.Value.ETag.ShouldNotBe(original.Value.ETag);
        var expected = await ReadAsync(sut, key);
        var stale = await sut.UpsertAsync(DocumentType, Write(key, "stale", original.Value.ETag));
        stale.Errors.ShouldContain(error => error is DocumentStoreConflictError);
        AssertUnchanged(await ReadAsync(sut, key), expected);
    }

    [Fact]
    public async Task UpsertAsync_VersionFromDifferentItem_ReturnsConflict()
    {
        // Arrange
        var sut = this.CreateProvider();
        var key = CreateKey();
        var first = await sut.UpsertAsync(DocumentType, Write(key, "first"));
        var other = await sut.UpsertAsync(DocumentType, Write(CreateKey(), "other"));
        first.IsSuccess.ShouldBeTrue();
        other.IsSuccess.ShouldBeTrue();
        var expected = await ReadAsync(sut, key);

        // Act
        var stale = await sut.UpsertAsync(DocumentType, Write(key, "wrong item", other.Value.ETag));

        // Assert
        stale.Errors.ShouldContain(error => error is DocumentStoreConflictError);
        AssertUnchanged(await ReadAsync(sut, key), expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpsertAsync_EmptyCondition_ReplacesExistingDocument(string etag)
    {
        // Arrange
        var sut = this.CreateProvider();
        var key = CreateKey();
        var original = await sut.UpsertAsync(DocumentType, Write(key, "original"));
        original.IsSuccess.ShouldBeTrue();

        // Act
        var replacement = await sut.UpsertAsync(DocumentType, Write(key, "replacement", etag));

        // Assert
        replacement.IsSuccess.ShouldBeTrue();
        replacement.Value.ETag.ShouldNotBe(original.Value.ETag);
        var stored = await ReadAsync(sut, key);
        stored.Content.ShouldBe(Encoding.UTF8.GetBytes("replacement"));
        stored.ETag.ShouldBe(replacement.Value.ETag);
    }

    [Theory]
    [InlineData(true, "original")]
    [InlineData(false, "*")]
    [InlineData(false, " * ")]
    public async Task UpsertAsync_InvalidConditions_RejectsBeforeMutation(bool createOnly, string etag)
    {
        // Arrange
        var sut = this.CreateProvider();
        var key = CreateKey();
        var original = await sut.UpsertAsync(DocumentType, Write(key, "original"));
        original.IsSuccess.ShouldBeTrue();
        var expected = await ReadAsync(sut, key);

        // Act
        var invalid = await sut.UpsertAsync(DocumentType, Write(key, "invalid") with
        {
            Options = new() { CreateOnly = createOnly, IfMatchETag = etag }
        });

        // Assert
        invalid.Errors.ShouldContain(error => error is DocumentStoreInvalidQueryError);
        AssertUnchanged(await ReadAsync(sut, key), expected);
    }

    [Fact]
    public async Task UpsertAsync_CreateOnlyWithExistingItem_ReturnsConflict()
    {
        // Arrange
        var sut = this.CreateProvider();
        var key = CreateKey();
        (await sut.UpsertAsync(DocumentType, Write(key, "original"))).IsSuccess.ShouldBeTrue();
        var expected = await ReadAsync(sut, key);

        // Act
        var result = await sut.UpsertAsync(DocumentType, Write(key, "replacement") with { Options = new() { CreateOnly = true } });

        // Assert
        result.Errors.ShouldContain(error => error is DocumentStoreConflictError);
        AssertUnchanged(await ReadAsync(sut, key), expected);
    }

    [Fact]
    public async Task UpsertAsync_TwoWritersWithOriginalVersion_CommitsExactlyOne()
    {
        // Arrange
        var sut = this.CreateProvider();
        var key = CreateKey();
        var original = await sut.UpsertAsync(DocumentType, Write(key, "original"));
        original.IsSuccess.ShouldBeTrue();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writes = new[] { Write(key, "writer-one", original.Value.ETag), Write(key, "writer-two", original.Value.ETag) };
        var tasks = writes.Select(write => Task.Run(async () =>
        {
            await start.Task;
            return await sut.UpsertAsync(DocumentType, write);
        })).ToArray();

        // Act
        start.SetResult();
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Count(result => result.IsSuccess).ShouldBe(1);
        results.Single(result => result.IsFailure).Errors.ShouldContain(error => error is DocumentStoreConflictError);
        var winner = Array.FindIndex(results, result => result.IsSuccess);
        var stored = await ReadAsync(sut, key);
        stored.Content.ShouldBe(writes[winner].Content);
        stored.ContentHash.ShouldBe(writes[winner].ContentHash);
        stored.ETag.ShouldBe(results[winner].Value.ETag);
        stored.Properties.Get<string>("label").ShouldBe(writes[winner].Properties.Get<string>("label"));
    }

    private static DocumentKey CreateKey() => new("concurrency", Guid.NewGuid().ToString("N"));

    private static StoredDocumentWrite Write(DocumentKey key, string content, string etag = null)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = ContentHashHelper.ComputeSha256(bytes);
        return new()
        {
            Key = key,
            Content = bytes,
            ContentHash = hash,
            StoredContentHash = hash,
            Properties = Properties(content),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            Options = new() { IfMatchETag = etag }
        };
    }

    private static PropertyBag Properties(string label)
    {
        var properties = new PropertyBag();
        properties.Set("label", label);
        return properties;
    }

    private static async Task<StoredDocument> ReadAsync(IDocumentStoreProvider provider, DocumentKey key)
    {
        var result = await provider.GetAsync(DocumentType, key, DateTimeOffset.UtcNow);
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        return result.Value;
    }

    private static void AssertUnchanged(StoredDocument actual, StoredDocument expected)
    {
        actual.Key.ShouldBe(expected.Key);
        actual.Content.ShouldBe(expected.Content);
        actual.ContentHash.ShouldBe(expected.ContentHash);
        actual.StoredContentHash.ShouldBe(expected.StoredContentHash);
        actual.ETag.ShouldBe(expected.ETag);
        actual.CreatedAt.ShouldBe(expected.CreatedAt);
        actual.LastModifiedAt.ShouldBe(expected.LastModifiedAt);
        actual.ExpiresAt.ShouldBe(expected.ExpiresAt);
        actual.Properties.OrderBy(pair => pair.Key).ShouldBe(expected.Properties.OrderBy(pair => pair.Key));
    }

    private sealed class ContractDocument;
}
