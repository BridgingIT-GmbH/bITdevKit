// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using System.Text;
using Application.Storage;
using Microsoft.Extensions.DependencyInjection;

[UnitTest("Application")]
public sealed class BlobStoreTransformBehaviorTests
{
    [Fact]
    public async Task CompressionBehavior_WithUploadAndDownload_RoundtripsLogicalContentAndScrubsMetadata()
    {
        // Arrange
        var key = new BlobKey("reports", "compressed.txt");
        var source = Encoding.UTF8.GetBytes(new string('a', 4096));
        var expectedHash = $"{BlobContentHash.Prefix}{HashHelper.ComputeSha256(source)}";
        var provider = new InMemoryBlobStoreProvider();
        var inner = new BlobStoreClient(InMemoryBlobStoreProvider.ProviderName, provider);
        var sut = new CompressionBlobStoreClientBehavior(inner);

        // Act
        var upload = await sut.UploadAsync(new BlobUpload
        {
            Key = key,
            Content = new MemoryStream(source),
            ContentType = ContentType.TXT,
            ExpectedContentHash = expectedHash,
            Properties = new PropertyBag { ["source"] = "unit-test" }
        });
        var rawProperties = await provider.GetPropertiesAsync(key);
        var rawDownload = await provider.DownloadAsync(key);
        var download = await sut.DownloadAsync(key);

        // Assert
        upload.IsSuccess.ShouldBeTrue();
        upload.Value.Length.ShouldBe(source.Length);
        upload.Value.ContentType.ShouldBe(ContentType.TXT);
        upload.Value.ContentHash.ShouldBe(expectedHash);
        upload.Value.Properties.Contains("bdk_compression_algorithm").ShouldBeFalse();

        rawProperties.Value.ContentType.ShouldBe(ContentType.BIN);
        rawProperties.Value.Properties.Contains("bdk_compression_algorithm").ShouldBeTrue();
        rawProperties.Value.Properties.Get<string>("bdk_compression_content_hash").ShouldBe(expectedHash);

        await using (rawDownload.Value)
        {
            using var rawBuffer = new MemoryStream();
            await rawDownload.Value.Content.CopyToAsync(rawBuffer);
            rawBuffer.ToArray().SequenceEqual(source).ShouldBeFalse();
        }

        await using (download.Value)
        {
            using var buffer = new MemoryStream();
            await download.Value.Content.CopyToAsync(buffer);
            buffer.ToArray().ShouldBe(source);
            download.Value.Info.Length.ShouldBe(source.Length);
            download.Value.Info.ContentType.ShouldBe(ContentType.TXT);
            download.Value.Info.ContentHash.ShouldBe(expectedHash);
            download.Value.Info.Properties.Get<string>("source").ShouldBe("unit-test");
            download.Value.Info.Properties.Keys.Any(key => key.StartsWith("bdk_compression", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task EncryptionBehavior_WithUploadAndDownload_RoundtripsLogicalContentAndScrubsMetadata()
    {
        // Arrange
        var key = new BlobKey("reports", "encrypted.txt");
        var source = Encoding.UTF8.GetBytes("sensitive content");
        var expectedHash = $"{BlobContentHash.Prefix}{HashHelper.ComputeSha256(source)}";
        var provider = new InMemoryBlobStoreProvider();
        var inner = new BlobStoreClient(InMemoryBlobStoreProvider.ProviderName, provider);
        var keyProvider = new DictionaryEncryptionKeyProvider(
            "unit-key",
            new Dictionary<string, byte[]> { ["unit-key"] = CreateKey() });
        var sut = new EncryptionBlobStoreClientBehavior(inner, keyProvider);

        // Act
        var upload = await sut.UploadAsync(new BlobUpload
        {
            Key = key,
            Content = new MemoryStream(source),
            ContentType = ContentType.TXT,
            ExpectedContentHash = expectedHash,
            Properties = new PropertyBag { ["source"] = "unit-test" }
        });
        var rawProperties = await provider.GetPropertiesAsync(key);
        var rawDownload = await provider.DownloadAsync(key);
        var download = await sut.DownloadAsync(key);

        // Assert
        upload.IsSuccess.ShouldBeTrue();
        upload.Value.Length.ShouldBe(source.Length);
        upload.Value.ContentType.ShouldBe(ContentType.TXT);
        upload.Value.ContentHash.ShouldBe(expectedHash);
        upload.Value.Properties.Contains("bdk_encryption_algorithm").ShouldBeFalse();

        rawProperties.Value.ContentType.ShouldBe(ContentType.BIN);
        rawProperties.Value.Properties.Contains("bdk_encryption_algorithm").ShouldBeTrue();
        rawProperties.Value.Properties.Get<string>("bdk_encryption_content_hash").ShouldBe(expectedHash);
        rawProperties.Value.Properties.Get<string>("bdk_encryption_key_id").ShouldBe("unit-key");

        await using (rawDownload.Value)
        {
            using var rawBuffer = new MemoryStream();
            await rawDownload.Value.Content.CopyToAsync(rawBuffer);
            rawBuffer.ToArray().SequenceEqual(source).ShouldBeFalse();
        }

        await using (download.Value)
        {
            using var buffer = new MemoryStream();
            await download.Value.Content.CopyToAsync(buffer);
            buffer.ToArray().ShouldBe(source);
            download.Value.Info.Length.ShouldBe(source.Length);
            download.Value.Info.ContentType.ShouldBe(ContentType.TXT);
            download.Value.Info.ContentHash.ShouldBe(expectedHash);
            download.Value.Info.Properties.Get<string>("source").ShouldBe("unit-test");
            download.Value.Info.Properties.Keys.Any(key => key.StartsWith("bdk_encryption", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task CompressionAndEncryptionBehaviors_WithRegistration_RoundtripInConfiguredOrder()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IEncryptionKeyProvider>(new DictionaryEncryptionKeyProvider(
            "unit-key",
            new Dictionary<string, byte[]> { ["unit-key"] = CreateKey() }));
        services.AddBlobStorage()
            .WithCompressionBehavior()
            .WithEncryptionBehavior()
            .WithInMemoryClient("reports");
        using var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetRequiredService<IBlobStoreClientFactory>().CreateClient("reports");
        var key = new BlobKey("reports", "secure-compressed.txt");
        var source = Encoding.UTF8.GetBytes(new string('z', 2048));

        // Act
        var upload = await client.UploadAsync(new BlobUpload
        {
            Key = key,
            Content = new MemoryStream(source),
            ContentType = ContentType.TXT
        });
        var download = await client.DownloadAsync(key);

        // Assert
        upload.IsSuccess.ShouldBeTrue();
        upload.Value.ContentType.ShouldBe(ContentType.TXT);
        upload.Value.Properties.Keys.Any(key => key.StartsWith("bdk", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();

        await using (download.Value)
        {
            using var buffer = new MemoryStream();
            await download.Value.Content.CopyToAsync(buffer);
            buffer.ToArray().ShouldBe(source);
            download.Value.Info.ContentType.ShouldBe(ContentType.TXT);
            download.Value.Info.Properties.Keys.Any(key => key.StartsWith("bdk", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task EncryptionBehavior_AfterActiveKeyRotation_UsesStoredKeyForExistingBlob()
    {
        // Arrange
        var key = new BlobKey("reports", "rotated.txt");
        var oldKey = CreateKey();
        var newKey = EncryptionHelper.GenerateAesKey();
        var provider = new InMemoryBlobStoreProvider();
        var inner = new BlobStoreClient(InMemoryBlobStoreProvider.ProviderName, provider);
        var writer = new EncryptionBlobStoreClientBehavior(
            inner,
            new DictionaryEncryptionKeyProvider(
                "old",
                new Dictionary<string, byte[]> { ["old"] = oldKey }));
        await writer.UploadAsync(new BlobUpload
        {
            Key = key,
            Content = new MemoryStream(Encoding.UTF8.GetBytes("encrypted before rotation"))
        });
        var reader = new EncryptionBlobStoreClientBehavior(
            inner,
            new DictionaryEncryptionKeyProvider(
                "new",
                new Dictionary<string, byte[]> { ["old"] = oldKey, ["new"] = newKey }));

        // Act
        var result = await reader.DownloadAsync(key);

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        await using (result.Value)
        using (var text = new StreamReader(result.Value.Content, Encoding.UTF8, leaveOpen: true))
        {
            (await text.ReadToEndAsync()).ShouldBe("encrypted before rotation");
        }
    }

    [Fact]
    public async Task CompressionBehavior_WithExpectedHashMismatch_ReturnsIntegrityFailureWithoutCommit()
    {
        // Arrange
        var key = new BlobKey("reports", "mismatch.txt");
        var provider = new InMemoryBlobStoreProvider();
        var inner = new BlobStoreClient(InMemoryBlobStoreProvider.ProviderName, provider);
        var sut = new CompressionBlobStoreClientBehavior(inner);

        // Act
        var result = await sut.UploadAsync(new BlobUpload
        {
            Key = key,
            Content = new MemoryStream(Encoding.UTF8.GetBytes("actual")),
            ExpectedContentHash = $"{BlobContentHash.Prefix}{new string('0', 64)}"
        });
        var exists = await provider.ExistsAsync(key);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreIntegrityError>().ShouldBeTrue();
        exists.Value.ShouldBeFalse();
    }

    [Fact]
    public async Task CompressionBehavior_WithPropertyUpdate_PreservesInternalMetadataAndReadability()
    {
        // Arrange
        var key = new BlobKey("reports", "updated.txt");
        var source = Encoding.UTF8.GetBytes("content");
        var provider = new InMemoryBlobStoreProvider();
        var inner = new BlobStoreClient(InMemoryBlobStoreProvider.ProviderName, provider);
        var sut = new CompressionBlobStoreClientBehavior(inner);
        await sut.UploadAsync(new BlobUpload
        {
            Key = key,
            Content = new MemoryStream(source),
            ContentType = ContentType.TXT
        });

        // Act
        var update = await sut.UpdatePropertiesAsync(new BlobPropertiesUpdate
        {
            Key = key,
            ContentType = ContentType.JSON,
            Properties = new PropertyBag { ["reviewed"] = true }
        });
        var rawProperties = await provider.GetPropertiesAsync(key);
        var download = await sut.DownloadAsync(key);

        // Assert
        update.IsSuccess.ShouldBeTrue();
        update.Value.ContentType.ShouldBe(ContentType.JSON);
        update.Value.Properties.Get<bool>("reviewed").ShouldBeTrue();
        update.Value.Properties.Contains("bdk_compression_algorithm").ShouldBeFalse();
        rawProperties.Value.Properties.Contains("bdk_compression_algorithm").ShouldBeTrue();

        await using (download.Value)
        {
            using var buffer = new MemoryStream();
            await download.Value.Content.CopyToAsync(buffer);
            buffer.ToArray().ShouldBe(source);
            download.Value.Info.ContentType.ShouldBe(ContentType.JSON);
            download.Value.Info.Properties.Get<bool>("reviewed").ShouldBeTrue();
        }
    }

    private static byte[] CreateKey() =>
        Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
}
