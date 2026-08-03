// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using System.Reflection;
using System.Text;
using Application.Storage;

public abstract class BlobStoreProviderContractTests
{
    protected abstract string ProviderName { get; }

    protected abstract IBlobStoreProvider CreateProvider(BlobStoreOptions options = null);

    [Fact]
    public async Task UploadAsync_WithValidContent_ReturnsBlobInfo()
    {
        // Arrange
        var sut = this.CreateClient();
        var key = CreateKey("upload-success.txt");

        // Act
        var result = await sut.UploadAsync(CreateUpload(key, "hello", ContentType.TXT));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Key.ShouldBe(key);
        result.Value.Length.ShouldBe(5);
        result.Value.ContentType?.MimeType().ShouldBe(ContentType.TXT.MimeType());
        result.Value.ContentHash.ShouldStartWith(BlobContentHash.Prefix);
        result.Value.ETag.ShouldNotBeNullOrWhiteSpace();
        result.Value.CreatedAt.ShouldNotBeNull();
        result.Value.LastModifiedAt.ShouldNotBeNull();
        result.Value.ExpiresAt.ShouldBeNull();
    }

    [Fact]
    public async Task UploadAsync_WithExpiration_RoundTripsExpirationThroughPropertiesAndListing()
    {
        // Arrange
        var sut = this.CreateClient(new BlobStoreOptions { AllowFullScans = true });
        var key = CreateKey("expires-at.txt");
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7).ToOffset(TimeSpan.FromHours(2));

        // Act
        var upload = await sut.UploadAsync(new BlobUpload
        {
            Key = key,
            Content = new MemoryStream(Encoding.UTF8.GetBytes("content")),
            ExpiresAt = expiresAt
        });
        var properties = await sut.GetPropertiesAsync(key);
        var page = await sut.ListPageAsync(new BlobQuery
        {
            Container = "contracts",
            Prefix = "expires-",
            Take = 10
        });

        // Assert
        upload.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, upload.Errors.Select(e => e.Message)));
        AssertSameExpiration(upload.Value.ExpiresAt, expiresAt);
        AssertSameExpiration(properties.Value.ExpiresAt, expiresAt);
        AssertSameExpiration(page.Value.Items.Single().ExpiresAt, expiresAt);
    }

    [Fact]
    public async Task ListContainersAsync_AfterUploads_ReturnsDistinctOrdinalContainerNames()
    {
        // Arrange
        var client = this.CreateClient();
        var catalog = client.ShouldBeAssignableTo<IBlobStoreContainerCatalog>();
        var first = await client.UploadAsync(CreateUpload(new BlobKey("contracts-secondary", "catalog-a.txt"), "a"));
        var second = await client.UploadAsync(CreateUpload(new BlobKey("contracts", "catalog-b.txt"), "b"));

        // Act
        var result = await catalog.ListContainersAsync();

        // Assert
        first.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, first.Errors.Select(error => error.Message)));
        second.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, second.Errors.Select(error => error.Message)));
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        result.Value.ShouldContain("contracts");
        result.Value.ShouldContain("contracts-secondary");
        result.Value.ShouldBe(result.Value.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task UploadAsync_WithExpectedContentHash_StoresMatchingContentHash()
    {
        // Arrange
        var sut = this.CreateClient();
        var key = CreateKey("expected-hash.txt");
        var content = Encoding.UTF8.GetBytes("hash-ok");
        var expectedHash = $"{BlobContentHash.Prefix}{HashHelper.ComputeSha256(content)}";

        // Act
        var upload = await sut.UploadAsync(new BlobUpload
        {
            Key = key,
            Content = new MemoryStream(content),
            ContentType = ContentType.TXT,
            ExpectedContentHash = expectedHash
        });
        var properties = await sut.GetPropertiesAsync(key);

        // Assert
        upload.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, upload.Errors.Select(e => e.Message)));
        upload.Value.ContentHash.ShouldBe(expectedHash);
        properties.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, properties.Errors.Select(e => e.Message)));
        properties.Value.ContentHash.ShouldBe(expectedHash);
    }

    [Fact]
    public async Task UploadAsync_WithFailIfExistsAndExistingBlob_ReturnsConflictAndKeepsOriginalContent()
    {
        // Arrange
        var sut = this.CreateClient();
        var key = CreateKey("fail-if-exists.txt");
        await sut.UploadAsync(CreateUpload(key, "original"));

        // Act
        var result = await sut.UploadAsync(new BlobUpload
        {
            Key = key,
            Content = new MemoryStream(Encoding.UTF8.GetBytes("replacement")),
            OverwriteMode = BlobOverwriteMode.FailIfExists
        });
        var download = await sut.DownloadAsync(key);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreConflictError>().ShouldBeTrue();
        download.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, download.Errors.Select(e => e.Message)));
        await using var value = download.Value;
        (await ReadAllTextAsync(value.Content)).ShouldBe("original");
    }

    [Fact]
    public async Task UploadAsync_WithPdfContentType_RoundTripsThroughContentTypeExtensions()
    {
        // Arrange
        var sut = this.CreateClient();
        var key = CreateKey("content-type.pdf");

        // Act
        var upload = await sut.UploadAsync(CreateUpload(key, "%PDF", ContentType.PDF));
        var properties = await sut.GetPropertiesAsync(key);

        // Assert
        upload.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, upload.Errors.Select(e => e.Message)));
        upload.Value.ContentType?.MimeType().ShouldBe(ContentType.PDF.MimeType());
        properties.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, properties.Errors.Select(e => e.Message)));
        properties.Value.ContentType?.MimeType().ShouldBe(ContentType.PDF.MimeType());
    }

    [Fact]
    public async Task UploadAsync_WithMutableSource_ClonesContent()
    {
        // Arrange
        var sut = this.CreateClient();
        var key = CreateKey("clone-source.bin");
        var bytes = Encoding.UTF8.GetBytes("original");

        // Act
        var upload = await sut.UploadAsync(new BlobUpload
        {
            Key = key,
            Content = new MemoryStream(bytes)
        });
        bytes[0] = (byte)'X';
        var download = await sut.DownloadAsync(key);

        // Assert
        upload.IsSuccess.ShouldBeTrue();
        download.IsSuccess.ShouldBeTrue();
        await using var value = download.Value;
        (await ReadAllTextAsync(value.Content)).ShouldBe("original");
    }

    [Fact]
    public async Task DownloadAsync_WithExistingBlob_ReturnsUploadedContent()
    {
        // Arrange
        var sut = this.CreateClient();
        var key = CreateKey("download.txt");
        await sut.UploadAsync(CreateUpload(key, "content"));

        // Act
        var result = await sut.DownloadAsync(key);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await using var download = result.Value;
        (await ReadAllTextAsync(download.Content)).ShouldBe("content");
        download.Info.Key.ShouldBe(key);
        download.Info.ContentHash.ShouldStartWith(BlobContentHash.Prefix);
    }

    [Fact]
    public async Task DownloadAsync_WithRepeatedDownloads_ReturnsNewStreamInstance()
    {
        // Arrange
        var sut = this.CreateClient();
        var key = CreateKey("new-stream.txt");
        await sut.UploadAsync(CreateUpload(key, "content"));

        // Act
        var first = await sut.DownloadAsync(key);
        var second = await sut.DownloadAsync(key);

        // Assert
        first.IsSuccess.ShouldBeTrue();
        second.IsSuccess.ShouldBeTrue();
        await using var firstDownload = first.Value;
        await using var secondDownload = second.Value;
        firstDownload.Content.ShouldNotBeSameAs(secondDownload.Content);
    }

    [Fact]
    public async Task DownloadAsync_WhenReturnedStreamIsMutated_DoesNotMutateStoredContent()
    {
        // Arrange
        var sut = this.CreateClient();
        var key = CreateKey("download-copy.txt");
        await sut.UploadAsync(CreateUpload(key, "original"));
        var first = await sut.DownloadAsync(key);

        // Act
        await using (var firstDownload = first.Value)
        {
            if (firstDownload.Content.CanWrite)
            {
                firstDownload.Content.Position = 0;
                await firstDownload.Content.WriteAsync(Encoding.UTF8.GetBytes("changed"));
            }
            else
            {
                await Should.ThrowAsync<NotSupportedException>(() =>
                    firstDownload.Content.WriteAsync(Encoding.UTF8.GetBytes("changed")).AsTask());
            }
        }

        var second = await sut.DownloadAsync(key);

        // Assert
        second.IsSuccess.ShouldBeTrue();
        await using var secondDownload = second.Value;
        (await ReadAllTextAsync(secondDownload.Content)).ShouldBe("original");
    }

    [Fact]
    public async Task DownloadAsync_WithMissingBlob_ReturnsNotFound()
    {
        // Arrange
        var sut = this.CreateClient();

        // Act
        var result = await sut.DownloadAsync(CreateKey("missing.txt"));

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreNotFoundError>().ShouldBeTrue();
    }

    [Fact]
    public async Task GetPropertiesAsync_WithExistingBlob_ReturnsMetadataWithoutContentSurface()
    {
        // Arrange
        var sut = this.CreateClient();
        var key = CreateKey("properties.txt");
        await sut.UploadAsync(CreateUpload(key, "content"));

        // Act
        var result = await sut.GetPropertiesAsync(key);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeOfType<BlobInfo>();
        typeof(BlobInfo).GetProperty("Content", BindingFlags.Instance | BindingFlags.Public).ShouldBeNull();
    }

    [Fact]
    public async Task UpdatePropertiesAsync_WithExistingBlob_UpdatesMetadataWithoutChangingContent()
    {
        // Arrange
        var sut = this.CreateClient();
        var key = CreateKey("update-properties.txt");
        var upload = await sut.UploadAsync(CreateUpload(key, "content", ContentType.TXT));
        var originalHash = upload.Value.ContentHash;

        // Act
        var update = await sut.UpdatePropertiesAsync(new BlobPropertiesUpdate
        {
            Key = key,
            ContentType = ContentType.CSV,
            IfMatchETag = upload.Value.ETag,
            Properties = new PropertyBag { ["reviewed"] = true }
        });
        var download = await sut.DownloadAsync(key);

        // Assert
        update.IsSuccess.ShouldBeTrue();
        update.Value.ContentType?.MimeType().ShouldBe(ContentType.CSV.MimeType());
        update.Value.Properties.Get<bool>("reviewed").ShouldBeTrue();
        update.Value.ContentHash.ShouldBe(originalHash);
        update.Value.ETag.ShouldNotBe(upload.Value.ETag);
        download.IsSuccess.ShouldBeTrue();
        await using var value = download.Value;
        (await ReadAllTextAsync(value.Content)).ShouldBe("content");
    }

    [Fact]
    public async Task UpdatePropertiesAsync_WithMissingBlob_ReturnsNotFound()
    {
        // Arrange
        var sut = this.CreateClient();

        // Act
        var result = await sut.UpdatePropertiesAsync(new BlobPropertiesUpdate
        {
            Key = CreateKey("missing-update.txt"),
            ContentType = ContentType.JSON
        });

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreNotFoundError>().ShouldBeTrue();
    }

    [Fact]
    public async Task UpdatePropertiesAsync_WithMismatchedETag_ReturnsConflict()
    {
        // Arrange
        var sut = this.CreateClient();
        var key = CreateKey("etag-conflict.txt");
        var upload = await sut.UploadAsync(CreateUpload(key, "content"));

        // Act
        var result = await sut.UpdatePropertiesAsync(new BlobPropertiesUpdate
        {
            Key = key,
            IfMatchETag = "\"stale\""
        });
        var properties = await sut.GetPropertiesAsync(key);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreConflictError>().ShouldBeTrue();
        properties.Value.ETag.ShouldBe(upload.Value.ETag);
    }

    [Fact]
    public async Task MetadataAndListOperations_WithExistingBlob_DoNotReadContent()
    {
        // Arrange
        var sut = this.CreateClient(new BlobStoreOptions { AllowFullScans = true });
        var key = CreateKey("metadata/no-content-read.txt");
        var upload = await sut.UploadAsync(CreateUpload(key, "content", ContentType.TXT));
        upload.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, upload.Errors.Select(e => e.Message)));
        this.ResetContentReadProbe();

        // Act
        var properties = await sut.GetPropertiesAsync(key);
        var exists = await sut.ExistsAsync(key);
        var update = await sut.UpdatePropertiesAsync(new BlobPropertiesUpdate
        {
            Key = key,
            ContentType = ContentType.JSON,
            IfMatchETag = upload.Value.ETag,
            Properties = new PropertyBag { ["reviewed"] = true }
        });
        var page = await sut.ListPageAsync(new BlobQuery
        {
            Container = "contracts",
            Prefix = "metadata/",
            Take = 10
        });

        // Assert
        properties.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, properties.Errors.Select(e => e.Message)));
        exists.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, exists.Errors.Select(e => e.Message)));
        exists.Value.ShouldBeTrue();
        update.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, update.Errors.Select(e => e.Message)));
        page.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, page.Errors.Select(e => e.Message)));
        page.Value.Items.Select(item => item.Key.Name).ShouldBe(["metadata/no-content-read.txt"]);
        this.AssertContentWasNotReadForMetadataOperations();
    }

    [Fact]
    public async Task ExistsAsync_WithExistingAndMissingBlobs_ReturnsExpectedValues()
    {
        // Arrange
        var sut = this.CreateClient();
        var existing = CreateKey("exists.txt");
        var missing = CreateKey("missing-exists.txt");
        await sut.UploadAsync(CreateUpload(existing, "content"));

        // Act
        var existingResult = await sut.ExistsAsync(existing);
        var missingResult = await sut.ExistsAsync(missing);

        // Assert
        existingResult.IsSuccess.ShouldBeTrue();
        existingResult.Value.ShouldBeTrue();
        missingResult.IsSuccess.ShouldBeTrue();
        missingResult.Value.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_WithExistingAndMissingBlobs_SucceedsIdempotently()
    {
        // Arrange
        var sut = this.CreateClient();
        var key = CreateKey("delete.txt");
        await sut.UploadAsync(CreateUpload(key, "content"));

        // Act
        var first = await sut.DeleteAsync(key);
        var second = await sut.DeleteAsync(key);
        var exists = await sut.ExistsAsync(key);

        // Assert
        first.IsSuccess.ShouldBeTrue();
        second.IsSuccess.ShouldBeTrue();
        exists.Value.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_WithConditionalETag_RejectsChangedSourceAndAcceptsCurrentSource()
    {
        // Arrange
        var sut = this.CreateClient();
        var key = CreateKey("delete-conditional.txt");
        var upload = await sut.UploadAsync(CreateUpload(key, "content"));

        // Act
        var conflict = await sut.DeleteAsync(key, new BlobDeleteOptions { IfMatchETag = "\"stale\"" });
        var existsAfterConflict = await sut.ExistsAsync(key);
        var deleted = await sut.DeleteAsync(key, new BlobDeleteOptions { IfMatchETag = upload.Value.ETag });

        // Assert
        conflict.IsFailure.ShouldBeTrue();
        conflict.HasError<BlobStoreConflictError>().ShouldBeTrue();
        existsAfterConflict.Value.ShouldBeTrue();
        deleted.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, deleted.Errors.Select(e => e.Message)));
    }

    [Fact]
    public async Task ListPageAsync_WithPrefix_ReturnsMatchingBlobsOnlyInNameOrder()
    {
        // Arrange
        var sut = this.CreateClient(new BlobStoreOptions { AllowFullScans = true });
        await sut.UploadAsync(CreateUpload(CreateKey("prefix/b.txt"), "b"));
        await sut.UploadAsync(CreateUpload(CreateKey("other/a.txt"), "other"));
        await sut.UploadAsync(CreateUpload(CreateKey("prefix/a.txt"), "a"));

        // Act
        var result = await sut.ListPageAsync(new BlobQuery
        {
            Container = "contracts",
            Prefix = "prefix/",
            Take = 10
        });

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Select(item => item.Key.Name).ShouldBe(["prefix/a.txt", "prefix/b.txt"]);
    }

    [Fact]
    public async Task ListPageAsync_WithResults_ReturnsBlobInfoOnly()
    {
        // Arrange
        var sut = this.CreateClient(new BlobStoreOptions { AllowFullScans = true });
        await sut.UploadAsync(CreateUpload(CreateKey("list-info.txt"), "content"));

        // Act
        var result = await sut.ListPageAsync(new BlobQuery
        {
            Container = "contracts",
            AllowFullScan = true
        });

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Single().ShouldBeOfType<BlobInfo>();
        typeof(BlobInfo).GetProperty("Content", BindingFlags.Instance | BindingFlags.Public).ShouldBeNull();
    }

    [Fact]
    public async Task ListPageAsync_WithFullScanWithoutApproval_ReturnsQueryTooBroad()
    {
        // Arrange
        var sut = this.CreateClient(new BlobStoreOptions { AllowFullScans = true });
        await sut.UploadAsync(CreateUpload(CreateKey("full-scan.txt"), "content"));

        // Act
        var result = await sut.ListPageAsync(new BlobQuery
        {
            Container = "contracts"
        });

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreQueryTooBroadError>().ShouldBeTrue();
    }

    [Fact]
    public async Task ListPageAsync_WithFullScanWhenGloballyDisabled_ReturnsQueryTooBroad()
    {
        // Arrange
        var sut = this.CreateClient(new BlobStoreOptions { AllowFullScans = false });

        // Act
        var result = await sut.ListPageAsync(new BlobQuery
        {
            Container = "contracts",
            AllowFullScan = true
        });

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreQueryTooBroadError>().ShouldBeTrue();
    }

    [Fact]
    public async Task ListPageAsync_WithApprovedFullScan_ReturnsResults()
    {
        // Arrange
        var sut = this.CreateClient(new BlobStoreOptions { AllowFullScans = true });
        await sut.UploadAsync(CreateUpload(CreateKey("full-scan-approved.txt"), "content"));

        // Act
        var result = await sut.ListPageAsync(new BlobQuery
        {
            Container = "contracts",
            AllowFullScan = true
        });

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldContain(item => item.Key.Name == "full-scan-approved.txt");
    }

    [Fact]
    public async Task ListPageAsync_WithContinuationToken_PagesAndBindsTokenToQuery()
    {
        // Arrange
        var sut = this.CreateClient(new BlobStoreOptions { AllowFullScans = true, DefaultTake = 2, MaxTake = 2 });
        await sut.UploadAsync(CreateUpload(CreateKey("paging/a.txt"), "a"));
        await sut.UploadAsync(CreateUpload(CreateKey("paging/b.txt"), "b"));
        await sut.UploadAsync(CreateUpload(CreateKey("paging/c.txt"), "c"));

        // Act
        var first = await sut.ListPageAsync(new BlobQuery
        {
            Container = "contracts",
            Prefix = "paging/",
            Take = 2
        });
        var second = await sut.ListPageAsync(new BlobQuery
        {
            Container = "contracts",
            Prefix = "paging/",
            Take = 2,
            ContinuationToken = first.Value.ContinuationToken
        });
        var reusedForDifferentQuery = await sut.ListPageAsync(new BlobQuery
        {
            Container = "contracts",
            Prefix = "other/",
            Take = 2,
            ContinuationToken = first.Value.ContinuationToken
        });

        // Assert
        first.IsSuccess.ShouldBeTrue();
        first.Value.Items.Select(item => item.Key.Name).ShouldBe(["paging/a.txt", "paging/b.txt"]);
        first.Value.ContinuationToken.ShouldNotBeNullOrWhiteSpace();
        second.IsSuccess.ShouldBeTrue();
        second.Value.Items.Select(item => item.Key.Name).ShouldBe(["paging/c.txt"]);
        second.Value.HasMore.ShouldBeFalse();
        reusedForDifferentQuery.IsFailure.ShouldBeTrue();
        reusedForDifferentQuery.HasError<BlobStoreInvalidContinuationTokenError>().ShouldBeTrue();
    }

    [Fact]
    public async Task ListPageAsync_WithTakeAboveMax_ReturnsPageSizeExceeded()
    {
        // Arrange
        var sut = this.CreateClient(new BlobStoreOptions { DefaultTake = 1, MaxTake = 2 });

        // Act
        var result = await sut.ListPageAsync(new BlobQuery
        {
            Container = "contracts",
            Prefix = "page-size/",
            Take = 3
        });

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStorePageSizeExceededError>().ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ListPageAsync_WithNonPositiveTake_ReturnsValidationFailure(int take)
    {
        // Arrange
        var sut = this.CreateClient();

        // Act
        var result = await sut.ListPageAsync(new BlobQuery
        {
            Container = "contracts",
            Prefix = "page-size/",
            Take = take
        });

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreValidationError>().ShouldBeTrue();
    }

    [Fact]
    public async Task UploadAsync_WhenMaxBlobSizeExceeded_DoesNotCommitPartialBlob()
    {
        // Arrange
        var sut = this.CreateClient(new BlobStoreOptions { MaxBlobSize = 3 });
        var key = CreateKey("too-large.bin");

        // Act
        var result = await sut.UploadAsync(new BlobUpload
        {
            Key = key,
            Content = new NonSeekableReadStream(Encoding.UTF8.GetBytes("1234"))
        });
        var exists = await sut.ExistsAsync(key);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreSizeLimitExceededError>().ShouldBeTrue();
        exists.Value.ShouldBeFalse();
    }

    [Fact]
    public async Task UploadAsync_WhenExpectedContentHashDoesNotMatch_DoesNotCommitPartialBlob()
    {
        // Arrange
        var sut = this.CreateClient();
        var key = CreateKey("hash-mismatch.bin");

        // Act
        var result = await sut.UploadAsync(new BlobUpload
        {
            Key = key,
            Content = new MemoryStream(Encoding.UTF8.GetBytes("content")),
            ExpectedContentHash = $"{BlobContentHash.Prefix}{new string('0', 64)}"
        });
        var exists = await sut.ExistsAsync(key);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreIntegrityError>().ShouldBeTrue();
        exists.Value.ShouldBeFalse();
    }

    [Fact]
    public void PublicBlobStoreClientContract_DoesNotExposeRangeOrResumableApis()
    {
        // Arrange
        var methodNames = typeof(IBlobStoreClient)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(method => method.Name)
            .ToArray();

        // Assert
        methodNames.ShouldNotContain(name => name.Contains("Range", StringComparison.OrdinalIgnoreCase));
        methodNames.ShouldNotContain(name => name.Contains("Resume", StringComparison.OrdinalIgnoreCase));
        methodNames.ShouldNotContain(name => name.Contains("Chunk", StringComparison.OrdinalIgnoreCase));
    }

    protected IBlobStoreClient CreateClient(BlobStoreOptions options = null)
    {
        options ??= new BlobStoreOptions();
        return new BlobStoreClient(this.ProviderName, this.CreateProvider(options), options);
    }

    protected virtual void ResetContentReadProbe()
    {
    }

    protected virtual void AssertContentWasNotReadForMetadataOperations()
    {
    }

    protected static BlobKey CreateKey(string name) => new("contracts", name);

    protected static BlobUpload CreateUpload(
        BlobKey key,
        string content,
        ContentType? contentType = null) =>
        new()
        {
            Key = key,
            Content = new MemoryStream(Encoding.UTF8.GetBytes(content)),
            ContentType = contentType,
            Properties = new PropertyBag { ["source"] = "contract" }
        };

    protected static async Task<string> ReadAllTextAsync(Stream stream)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

        return await reader.ReadToEndAsync();
    }

    private static void AssertSameExpiration(DateTimeOffset? actual, DateTimeOffset expected)
    {
        actual.ShouldNotBeNull();
        actual.Value.Offset.ShouldBe(TimeSpan.Zero);
        actual.Value.ToUniversalTime()
            .ShouldBeInRange(expected.ToUniversalTime().AddSeconds(-1), expected.ToUniversalTime().AddSeconds(1));
    }

    private sealed class NonSeekableReadStream(byte[] content) : MemoryStream(content)
    {
        public override bool CanSeek => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
    }
}
