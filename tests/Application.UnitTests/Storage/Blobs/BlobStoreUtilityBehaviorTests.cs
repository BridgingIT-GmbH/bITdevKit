// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using System.Text;
using Application.Storage;
using Microsoft.Extensions.DependencyInjection;

[UnitTest("Application")]
public sealed class BlobStoreUtilityBehaviorTests
{
    [Fact]
    public async Task ContentTypeDetectionBehavior_WithMissingContentType_DetectsFromBlobNameExtension()
    {
        // Arrange
        var provider = new InMemoryBlobStoreProvider();
        var inner = new BlobStoreClient(InMemoryBlobStoreProvider.ProviderName, provider);
        var sut = new ContentTypeDetectionBlobStoreClientBehavior(inner);
        var key = new BlobKey("reports", "monthly/report.pdf");

        // Act
        var result = await sut.UploadAsync(new BlobUpload
        {
            Key = key,
            Content = new MemoryStream(Encoding.UTF8.GetBytes("pdf"))
        });

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ContentType.ShouldBe(ContentType.PDF);
    }

    [Fact]
    public async Task ContentTypeDetectionBehavior_WithExplicitContentType_DoesNotOverride()
    {
        // Arrange
        var provider = new InMemoryBlobStoreProvider();
        var inner = new BlobStoreClient(InMemoryBlobStoreProvider.ProviderName, provider);
        var sut = new ContentTypeDetectionBlobStoreClientBehavior(inner);
        var key = new BlobKey("reports", "monthly/report.pdf");

        // Act
        var result = await sut.UploadAsync(new BlobUpload
        {
            Key = key,
            Content = new MemoryStream(Encoding.UTF8.GetBytes("plain")),
            ContentType = ContentType.TXT
        });

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ContentType.ShouldBe(ContentType.TXT);
    }

    [Fact]
    public async Task ContentTypeDetectionBehavior_WithExtensionlessName_DoesNotInferText()
    {
        // Arrange
        var provider = new InMemoryBlobStoreProvider();
        var inner = new BlobStoreClient(InMemoryBlobStoreProvider.ProviderName, provider);
        var sut = new ContentTypeDetectionBlobStoreClientBehavior(inner);
        var key = new BlobKey("reports", "monthly/report");

        // Act
        var result = await sut.UploadAsync(new BlobUpload
        {
            Key = key,
            Content = new MemoryStream(Encoding.UTF8.GetBytes("content"))
        });

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ContentType.ShouldBeNull();
    }

    [Fact]
    public async Task ChecksumVerificationBehavior_WithMatchingHash_ReturnsVerifiedDownload()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes("verified");
        var hash = $"{BlobContentHash.Prefix}{HashHelper.ComputeSha256(content)}";
        var inner = new ScriptedBlobStoreClient
        {
            Download = key => Result<BlobDownload>.Success(new BlobDownload
            {
                Content = new MemoryStream(content),
                Info = new BlobInfo
                {
                    Key = key,
                    Length = content.Length,
                    ContentHash = hash
                }
            })
        };
        var sut = new ChecksumVerificationBlobStoreClientBehavior(inner);

        // Act
        var result = await sut.DownloadAsync(new BlobKey("reports", "verified.txt"));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await using (result.Value)
        {
            using var buffer = new MemoryStream();
            await result.Value.Content.CopyToAsync(buffer);
            buffer.ToArray().ShouldBe(content);
            result.Value.Info.ContentHash.ShouldBe(hash);
        }
    }

    [Fact]
    public async Task ChecksumVerificationBehavior_WithMismatchedHash_ReturnsIntegrityFailure()
    {
        // Arrange
        var inner = new ScriptedBlobStoreClient
        {
            Download = key => Result<BlobDownload>.Success(new BlobDownload
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes("tampered")),
                Info = new BlobInfo
                {
                    Key = key,
                    Length = 8,
                    ContentHash = $"{BlobContentHash.Prefix}{new string('0', 64)}"
                }
            })
        };
        var sut = new ChecksumVerificationBlobStoreClientBehavior(inner);

        // Act
        var result = await sut.DownloadAsync(new BlobKey("reports", "tampered.txt"));

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreIntegrityError>().ShouldBeTrue();
    }

    [Fact]
    public async Task ChecksumVerificationBehavior_WithMissingHashAndDefaultOptions_ReturnsIntegrityFailure()
    {
        // Arrange
        var inner = new ScriptedBlobStoreClient
        {
            Download = key => Result<BlobDownload>.Success(new BlobDownload
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes("missing")),
                Info = new BlobInfo { Key = key, Length = 7 }
            })
        };
        var sut = new ChecksumVerificationBlobStoreClientBehavior(inner);

        // Act
        var result = await sut.DownloadAsync(new BlobKey("reports", "missing.txt"));

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreIntegrityError>().ShouldBeTrue();
    }

    [Fact]
    public async Task AddBlobStorage_WithUtilityBehaviors_RegistersNamedClient()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBlobStorage()
            .WithContentTypeDetectionBehavior()
            .WithChecksumVerificationBehavior()
            .WithInMemoryClient("reports");
        using var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetRequiredService<IBlobStoreClientFactory>().CreateClient("reports");
        var key = new BlobKey("reports", "detected.txt");

        // Act
        var upload = await client.UploadAsync(new BlobUpload
        {
            Key = key,
            Content = new MemoryStream(Encoding.UTF8.GetBytes("content"))
        });
        var download = await client.DownloadAsync(key);

        // Assert
        upload.IsSuccess.ShouldBeTrue();
        upload.Value.ContentType.ShouldBe(ContentType.TXT);
        download.IsSuccess.ShouldBeTrue();
        await download.Value.DisposeAsync();
    }

    private sealed class ScriptedBlobStoreClient : IBlobStoreClient
    {
        public Func<BlobKey, Result<BlobDownload>> Download { get; init; }

        public Task<Result<BlobInfo>> UploadAsync(BlobUpload upload, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<BlobInfo>.Success(new BlobInfo { Key = upload.Key }));

        public Task<Result<BlobDownload>> DownloadAsync(BlobKey key, CancellationToken cancellationToken = default) =>
            Task.FromResult(this.Download?.Invoke(key) ?? Result<BlobDownload>.Failure(new BlobStoreNotFoundError(key)));

        public Task<Result<BlobInfo>> GetPropertiesAsync(BlobKey key, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<BlobInfo>.Success(new BlobInfo { Key = key }));

        public Task<Result<BlobInfo>> UpdatePropertiesAsync(BlobPropertiesUpdate update, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<BlobInfo>.Success(new BlobInfo { Key = update.Key }));

        public Task<Result<bool>> ExistsAsync(BlobKey key, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<bool>.Success(true));

        public Task<Result<BlobPage>> ListPageAsync(BlobQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<BlobPage>.Success(new BlobPage()));

        public Task<Result> DeleteAsync(
            BlobKey key,
            BlobDeleteOptions options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());
    }
}
