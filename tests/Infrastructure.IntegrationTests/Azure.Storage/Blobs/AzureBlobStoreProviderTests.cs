// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.Azure.Storage;

using System.Text;
using Application.Storage;
using Infrastructure.Azure;
using Microsoft.Extensions.DependencyInjection;
using global::Azure;
using global::Azure.Storage.Blobs;

[IntegrationTest("Infrastructure")]
public sealed class AzureBlobStoreProviderTests
{
    [Fact]
    public void WithAzureBlobClient_WhenRegistered_ResolvesNamedClient()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new BlobServiceClient("UseDevelopmentStorage=true"));
        services.AddBlobStorage()
            .WithAzureBlobClient("reports");

        using var serviceProvider = services.BuildServiceProvider();

        // Act
        var factory = serviceProvider.GetRequiredService<IBlobStoreClientFactory>();
        var client = factory.CreateClient("reports");

        // Assert
        client.ShouldNotBeNull();
        factory.GetRegistrations().Single().ProviderName.ShouldBe(AzureBlobStoreProvider.ProviderName);
    }

    [Fact]
    public async Task UploadAsync_WithValidContent_UsesBackendUploadAndStoresHashMetadata()
    {
        // Arrange
        var backend = new RecordingAzureBlobStoreBackend();
        var sut = CreateProvider(backend);

        // Act
        var result = await sut.UploadAsync(CreateUpload("content"));

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        backend.UploadCalls.ShouldBe(1);
        backend.SetPropertiesCalls.ShouldBe(0);
        backend.LastMetadata.ShouldContainKey("bdk_contenthash");
        backend.LastMetadata["bdk_contenthash"].ShouldStartWith(BlobContentHash.Prefix);
        result.Value.ContentHash.ShouldBe(backend.LastMetadata["bdk_contenthash"]);
    }

    [Fact]
    public async Task UploadAsync_WithExpiration_StoresExpirationMetadataAndTag()
    {
        // Arrange
        var backend = new RecordingAzureBlobStoreBackend();
        var sut = CreateProvider(backend);
        var expiresAt = DateTimeOffset.UtcNow.AddDays(1);

        // Act
        var result = await sut.UploadAsync(CreateUpload("content", expiresAt: expiresAt));

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.ExpiresAt.ShouldBe(expiresAt.ToUniversalTime());
        backend.LastMetadata.ShouldContainKey("bdk_expiresat");
        backend.LastTags.ShouldContainKey("bdk_expiresat");
        backend.LastTags["bdk_expiresat"].ShouldBe(backend.LastMetadata["bdk_expiresat"]);
    }

    [Fact]
    public async Task UploadAsync_WhenMaxBlobSizeExceeded_FailsBeforeBackendUpload()
    {
        // Arrange
        var backend = new RecordingAzureBlobStoreBackend(new BlobStoreOptions { MaxBlobSize = 3 });
        var sut = CreateProvider(backend);

        // Act
        var result = await sut.UploadAsync(CreateUpload("content"));

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreSizeLimitExceededError>().ShouldBeTrue();
        backend.UploadCalls.ShouldBe(0);
    }

    [Fact]
    public async Task UploadAsync_WithFailIfExists_MapsNativeConditionalUpload()
    {
        // Arrange
        var backend = new RecordingAzureBlobStoreBackend();
        var sut = CreateProvider(backend);

        // Act
        var result = await sut.UploadAsync(CreateUpload("content", overwriteMode: BlobOverwriteMode.FailIfExists));

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        backend.LastFailIfExists.ShouldBeTrue();
    }

    [Fact]
    public async Task UploadAsync_WithExpectedHashAndNonSeekableStream_FailsWithoutCommit()
    {
        // Arrange
        var backend = new RecordingAzureBlobStoreBackend();
        var sut = CreateProvider(backend);
        await sut.UploadAsync(CreateUpload("original"));

        // Act
        var result = await sut.UploadAsync(new BlobUpload
        {
            Key = new BlobKey("reports", "file.txt"),
            Content = new NonSeekableReadStream(Encoding.UTF8.GetBytes("content")),
            ExpectedContentHash = $"{BlobContentHash.Prefix}{new string('0', 64)}"
        });
        var download = await sut.DownloadAsync(new BlobKey("reports", "file.txt"));

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreIntegrityError>().ShouldBeTrue();
        backend.UploadCalls.ShouldBe(2);
        await using var blob = download.Value;
        (await ReadAllTextAsync(blob.Content)).ShouldBe("original");
    }

    [Fact]
    public async Task UploadAsync_WhenCanceledAfterPartialStreaming_PreservesCommittedBlob()
    {
        // Arrange
        var backend = new RecordingAzureBlobStoreBackend();
        var sut = CreateProvider(backend);
        await sut.UploadAsync(CreateUpload("original"));
        using var cancellation = new CancellationTokenSource();

        // Act
        var action = async () => await sut.UploadAsync(new BlobUpload
        {
            Key = new BlobKey("reports", "file.txt"),
            Content = new CancelAfterFirstReadStream(Encoding.UTF8.GetBytes("replacement"), cancellation)
        }, cancellation.Token);
        await action.ShouldThrowAsync<OperationCanceledException>();
        var download = await sut.DownloadAsync(new BlobKey("reports", "file.txt"));

        // Assert
        await using var blob = download.Value;
        (await ReadAllTextAsync(blob.Content)).ShouldBe("original");
    }

    [Fact]
    public async Task UploadAsync_WithReservedMetadataKey_ReturnsSerializationError()
    {
        // Arrange
        var backend = new RecordingAzureBlobStoreBackend();
        var sut = CreateProvider(backend);

        // Act
        var result = await sut.UploadAsync(new BlobUpload
        {
            Key = new BlobKey("reports", "file.txt"),
            Content = new MemoryStream(Encoding.UTF8.GetBytes("content")),
            Properties = new PropertyBag
            {
                ["bdk_contenthash"] = "caller-value"
            }
        });

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreSerializationError>().ShouldBeTrue();
        backend.UploadCalls.ShouldBe(0);
    }

    [Fact]
    public async Task UploadAsync_WithComplexMetadataValue_ReturnsSerializationError()
    {
        // Arrange
        var backend = new RecordingAzureBlobStoreBackend();
        var sut = CreateProvider(backend);

        // Act
        var result = await sut.UploadAsync(new BlobUpload
        {
            Key = new BlobKey("reports", "file.txt"),
            Content = new MemoryStream(Encoding.UTF8.GetBytes("content")),
            Properties = new PropertyBag
            {
                ["complex"] = new { Value = "unsupported" }
            }
        });

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreSerializationError>().ShouldBeTrue();
        backend.UploadCalls.ShouldBe(0);
    }

    [Fact]
    public async Task DownloadAsync_WithExistingBlob_ReturnsReadableStream()
    {
        // Arrange
        var backend = new RecordingAzureBlobStoreBackend();
        var sut = CreateProvider(backend);
        await sut.UploadAsync(CreateUpload("content"));

        // Act
        var result = await sut.DownloadAsync(new BlobKey("reports", "file.txt"));

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        await using var download = result.Value;
        download.Content.CanRead.ShouldBeTrue();
        (await ReadAllTextAsync(download.Content)).ShouldBe("content");
    }

    [Fact]
    public async Task PropertiesRoundTrip_MapsMetadataContentTypeAndETag()
    {
        // Arrange
        var backend = new RecordingAzureBlobStoreBackend();
        var sut = CreateProvider(backend);
        var upload = await sut.UploadAsync(CreateUpload("content"));

        // Act
        var update = await sut.UpdatePropertiesAsync(new BlobPropertiesUpdate
        {
            Key = new BlobKey("reports", "file.txt"),
            ContentType = ContentType.JSON,
            IfMatchETag = upload.Value.ETag,
            Properties = new PropertyBag
            {
                ["reviewed"] = true,
                ["source"] = "azure-test",
                ["looksBoolean"] = "true",
                ["looksNumeric"] = "00123"
            }
        });
        var properties = await sut.GetPropertiesAsync(new BlobKey("reports", "file.txt"));

        // Assert
        update.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, update.Errors.Select(e => e.Message)));
        backend.LastIfMatchETag.ShouldBe(upload.Value.ETag);
        properties.Value.ContentType?.MimeType().ShouldBe(ContentType.JSON.MimeType());
        properties.Value.Properties.Get<bool>("reviewed").ShouldBeTrue();
        properties.Value.Properties.Get<string>("source").ShouldBe("azure-test");
        properties.Value.Properties.Get<string>("looksBoolean").ShouldBe("true");
        properties.Value.Properties.Get<string>("looksNumeric").ShouldBe("00123");
        properties.Value.ETag.ShouldNotBe(upload.Value.ETag);
    }

    [Fact]
    public async Task UpdatePropertiesAsync_WithExpiration_UpdatesExpirationMetadataAndTag()
    {
        // Arrange
        var backend = new RecordingAzureBlobStoreBackend();
        var sut = CreateProvider(backend);
        var upload = await sut.UploadAsync(CreateUpload("content"));
        var expiresAt = DateTimeOffset.UtcNow.AddDays(2);

        // Act
        var update = await sut.UpdatePropertiesAsync(new BlobPropertiesUpdate
        {
            Key = new BlobKey("reports", "file.txt"),
            ContentType = ContentType.JSON,
            IfMatchETag = upload.Value.ETag,
            ExpiresAt = expiresAt,
            Properties = new PropertyBag
            {
                ["source"] = "azure-test"
            }
        });

        // Assert
        update.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, update.Errors.Select(e => e.Message)));
        update.Value.ExpiresAt.ShouldBe(expiresAt.ToUniversalTime());
        backend.LastMetadata.ShouldContainKey("bdk_expiresat");
        backend.LastTags.ShouldContainKey("bdk_expiresat");
    }

    [Fact]
    public async Task ListPageAsync_WithPrefix_UsesNativePrefix()
    {
        // Arrange
        var backend = new RecordingAzureBlobStoreBackend();
        var sut = CreateProvider(backend);
        await sut.UploadAsync(CreateUpload("a", "prefix/a.txt"));
        await sut.UploadAsync(CreateUpload("b", "prefix/b.txt"));
        await sut.UploadAsync(CreateUpload("c", "other/c.txt"));

        // Act
        var result = await sut.ListPageAsync(new BlobQuery
        {
            Container = "reports",
            Prefix = "prefix/"
        });

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        backend.LastListPrefix.ShouldBe("prefix/");
        result.Value.Items.Select(e => e.Key.Name).ShouldBe(["prefix/a.txt", "prefix/b.txt"]);
    }

    [Fact]
    public async Task ListPageAsync_WithContinuation_WrapsNativeToken()
    {
        // Arrange
        var backend = new RecordingAzureBlobStoreBackend(new BlobStoreOptions { DefaultTake = 1, MaxTake = 1 });
        var sut = CreateProvider(backend);
        await sut.UploadAsync(CreateUpload("a", "paging/a.txt"));
        await sut.UploadAsync(CreateUpload("b", "paging/b.txt"));

        // Act
        var first = await sut.ListPageAsync(new BlobQuery
        {
            Container = "reports",
            Prefix = "paging/",
            Take = 1
        });
        var token = BlobContinuationTokenSerializer.Deserialize(first.Value.ContinuationToken);
        var second = await sut.ListPageAsync(new BlobQuery
        {
            Container = "reports",
            Prefix = "paging/",
            Take = 1,
            ContinuationToken = first.Value.ContinuationToken
        });

        // Assert
        first.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, first.Errors.Select(e => e.Message)));
        first.Value.ContinuationToken.ShouldNotBe("native:1");
        token.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, token.Errors.Select(e => e.Message)));
        token.Value.NativeToken.ShouldBe("native:1");
        second.Value.Items.Single().Key.Name.ShouldBe("paging/b.txt");
    }

    [Fact]
    public async Task ProviderRequestFailure_ReturnsProviderError()
    {
        // Arrange
        var backend = new RecordingAzureBlobStoreBackend
        {
            NextException = new RequestFailedException(500, "storage failed")
        };
        var sut = CreateProvider(backend);

        // Act
        var result = await sut.GetPropertiesAsync(new BlobKey("reports", "file.txt"));

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreProviderError>().ShouldBeTrue();
    }

    [Fact]
    public async Task SweepExpiredAsync_WithExpiredTaggedBlobs_DeletesExpiredOnly()
    {
        // Arrange
        var backend = new RecordingAzureBlobStoreBackend();
        var sut = CreateProvider(backend);
        var now = DateTimeOffset.UtcNow;
        await sut.UploadAsync(new BlobUpload
        {
            Key = new BlobKey("reports", "expired.txt"),
            Content = new MemoryStream(Encoding.UTF8.GetBytes("expired")),
            ExpiresAt = now.AddMinutes(-1)
        });
        await sut.UploadAsync(new BlobUpload
        {
            Key = new BlobKey("reports", "future.txt"),
            Content = new MemoryStream(Encoding.UTF8.GetBytes("future")),
            ExpiresAt = now.AddMinutes(1)
        });

        // Act
        var result = await sut.SweepExpiredAsync(new BlobRetentionSweepRequest
        {
            StoreName = "azure",
            ProviderName = AzureBlobStoreProvider.ProviderName,
            ExpiresOnOrBefore = now,
            BatchSize = 10,
            MaxBatches = 2
        });
        var expiredExists = await sut.ExistsAsync(new BlobKey("reports", "expired.txt"));
        var futureExists = await sut.ExistsAsync(new BlobKey("reports", "future.txt"));

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.DeletedCount.ShouldBe(1);
        result.Value.DeletedKeys.ShouldBe([new BlobKey("reports", "expired.txt")]);
        expiredExists.Value.ShouldBeFalse();
        futureExists.Value.ShouldBeTrue();
    }

    private static AzureBlobStoreProvider CreateProvider(
        RecordingAzureBlobStoreBackend backend) => backend;

    private static BlobUpload CreateUpload(
        string content,
        string name = "file.txt",
        BlobOverwriteMode overwriteMode = BlobOverwriteMode.Overwrite,
        DateTimeOffset? expiresAt = null) =>
        new()
        {
            Key = new BlobKey("reports", name),
            Content = new MemoryStream(Encoding.UTF8.GetBytes(content)),
            ContentType = ContentType.TXT,
            OverwriteMode = overwriteMode,
            ExpiresAt = expiresAt,
            Properties = new PropertyBag
            {
                ["source"] = "azure-test"
            }
        };

    private static async Task<string> ReadAllTextAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
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

    private sealed class CancelAfterFirstReadStream(
        byte[] content,
        CancellationTokenSource cancellation) : MemoryStream(content)
    {
        private bool hasRead;

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            var read = await base.ReadAsync(buffer, cancellationToken);
            if (!this.hasRead && read > 0)
            {
                this.hasRead = true;
                cancellation.Cancel();
            }

            return read;
        }
    }
}
