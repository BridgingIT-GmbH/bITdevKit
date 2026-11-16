// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using System.IO.Compression;
using Application.Storage;
using Microsoft.Extensions.DependencyInjection;

[UnitTest("Application")]
public sealed class BlobStoreValueCodecTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 128)]
    [InlineData(2, 1)]
    [InlineData(2, 128)]
    public async Task UploadAndDownloadAsync_VersionedCodecAndCompression_RestoresListUsingStoredFormat(int version, int count)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddValueCodecs(codecs => codecs
            .WithCodec<List<InventoryItem>, InventoryCodecV1>()
            .WithCodec<List<InventoryItem>, InventoryCodecV2>(isDefault: true));
        var rawProvider = new InMemoryBlobStoreProvider();
        services.AddBlobStorage()
            .WithCompressionBehavior()
            .RegisterClient("inventory", _ => rawProvider, providerName: InMemoryBlobStoreProvider.ProviderName);
        await using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<ValueCodecRegistry>();
        var currentWriter = provider.GetRequiredService<IValueCodec<List<InventoryItem>>>();
        currentWriter.FormatVersion.ShouldBe(2);
        registry.TryGet<List<InventoryItem>>(new("inventory", version), out var writer).ShouldBeTrue();
        var expected = Enumerable.Range(0, count).Select(index => new InventoryItem($"P-{index:D4}", index * 10)).ToList();
        var key = new BlobKey("snapshots", $"warehouse-42/v{version}.bin");
        var sut = provider.GetRequiredService<IBlobStoreClientFactory>().CreateClient("inventory");
        await using var content = new MemoryStream();

        // Act
        var encoded = await writer.EncodeAsync(expected, content);
        encoded.IsSuccess.ShouldBeTrue();
        var encodedBytes = content.ToArray();
        content.Position = 0;
        var uploaded = await sut.UploadAsync(new BlobUpload
        {
            Key = key,
            Content = content,
            ContentType = ContentType.BIN,
            Properties = new PropertyBag
            {
                ["codec_id"] = writer.FormatId,
                ["codec_version"] = writer.FormatVersion
            }
        });
        uploaded.IsSuccess.ShouldBeTrue();
        var raw = await rawProvider.DownloadAsync(key);
        var downloaded = await sut.DownloadAsync(key);

        // Assert
        raw.IsSuccess.ShouldBeTrue();
        await using (var stored = raw.Value)
        {
            stored.Info.Properties.Get<string>("bdk_compression_algorithm").ShouldBe("gzip");
            stored.Info.Properties.Get<string>("codec_id").ShouldBe("inventory");
            stored.Info.Properties.Get<int>("codec_version").ShouldBe(version);
            using var gzip = new GZipStream(stored.Content, CompressionMode.Decompress, leaveOpen: true);
            using var uncompressed = new MemoryStream();
            await gzip.CopyToAsync(uncompressed);
            uncompressed.ToArray().ShouldBe(encodedBytes);
        }

        downloaded.IsSuccess.ShouldBeTrue();
        await using var download = downloaded.Value;
        var properties = download.Info.Properties;
        properties.TryGet<string>("codec_id", out var formatId).ShouldBeTrue();
        properties.TryGet<int>("codec_version", out var formatVersion).ShouldBeTrue();
        formatVersion.ShouldBe(version);
        registry.TryGet<List<InventoryItem>>(new(formatId, formatVersion), out var reader).ShouldBeTrue();
        var decoded = await reader.DecodeAsync(download.Content);
        decoded.IsSuccess.ShouldBeTrue();
        decoded.Value.ShouldBe(expected);
        properties.Keys.Any(name => name.StartsWith("bdk_compression", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();
        registry.TryGet<List<InventoryItem>>(new(formatId, 99), out _).ShouldBeFalse();
        registry.TryGet<InventoryItem>(new(formatId, formatVersion), out _).ShouldBeFalse();
    }

    public sealed record InventoryItem(string ProductCode, int Quantity);

    public class InventoryCodecV1 : IValueCodec<List<InventoryItem>>
    {
        private readonly SystemTextJsonValueCodec<List<InventoryItem>> inner = new();

        public string FormatId => "inventory";

        public virtual int FormatVersion => 1;

        public ValueCodecCapabilities Capabilities => this.inner.Capabilities;

        public async ValueTask<Result> EncodeAsync(List<InventoryItem> value, Stream destination, CancellationToken cancellationToken = default)
        {
            await destination.WriteAsync(new byte[] { (byte)this.FormatVersion }, cancellationToken);
            return await this.inner.EncodeAsync(value, destination, cancellationToken);
        }

        public async ValueTask<Result<List<InventoryItem>>> DecodeAsync(Stream source, CancellationToken cancellationToken = default)
        {
            var header = new byte[1];
            if (await source.ReadAsync(header, cancellationToken) != 1 || header[0] != this.FormatVersion)
            {
                return Result<List<InventoryItem>>.Failure(new ValueCodecError("InvalidVersion", "The codec version does not match the payload."));
            }

            return await this.inner.DecodeAsync(source, cancellationToken);
        }
    }

    public sealed class InventoryCodecV2 : InventoryCodecV1
    {
        public override int FormatVersion => 2;
    }
}