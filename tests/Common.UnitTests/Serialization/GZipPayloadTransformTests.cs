// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Serialization;

using System.IO.Compression;
using System.Text.Json;

/// <summary>Checks shared gzip streaming, format interoperability, parameter validation and ownership.</summary>
public sealed class GZipPayloadTransformTests
{
    /// <summary>Every supported encoding level interoperates with BCL gzip readers without requiring a seekable source.</summary>
    [Theory]
    [InlineData(CompressionLevel.NoCompression)]
    [InlineData(CompressionLevel.Fastest)]
    [InlineData(CompressionLevel.Optimal)]
    [InlineData(CompressionLevel.SmallestSize)]
    public async Task Encoding_SupportedLevels_ProducesForwardOnlyInteroperableGZip(CompressionLevel level)
    {
        var sut = new GZipPayloadTransform(level);
        var bytes = Enumerable.Range(0, 180000).Select(index => (byte)(index % 251)).ToArray();
        using var source = new ForwardOnlyReadStream(new MemoryStream(bytes));
        using var stored = new MemoryStream();

        await using (var encoder = sut.CreateEncodingStream(stored, ReadOnlyMemory<byte>.Empty))
        {
            encoder.CanSeek.ShouldBeFalse();
            await source.CopyToAsync(encoder);
        }

        stored.CanWrite.ShouldBeTrue();
        stored.Position = 0;
        using var bclReader = new GZipStream(stored, CompressionMode.Decompress, true);
        using var restored = new MemoryStream();
        await bclReader.CopyToAsync(restored);
        restored.ToArray().ShouldBe(bytes);
        sut.FormatId.ShouldBe("gzip");
        sut.FormatVersion.ShouldBe(1);
        sut.MaximumStateBytes.ShouldBeGreaterThan(0);
    }

    /// <summary>The decoder consumes existing helper-produced gzip bytes through a nonseekable stream.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(180000)]
    public async Task Decoding_ExistingGZipFormat_ReadsForwardOnlyWithoutOwningSource(int length)
    {
        var bytes = Enumerable.Range(0, length).Select(index => (byte)(index % 251)).ToArray();
        var legacy = await CompressionHelper.CompressAsync(bytes);
        using var source = new ForwardOnlyReadStream(new MemoryStream(legacy));
        var sut = new GZipPayloadTransform();
        using var destination = new MemoryStream();

        await using (var decoder = sut.CreateDecodingStream(source, ReadOnlyMemory<byte>.Empty))
        {
            decoder.CanSeek.ShouldBeFalse();
            await decoder.CopyToAsync(destination);
        }

        destination.ToArray().ShouldBe(bytes);
        source.CanRead.ShouldBeTrue();
    }

    /// <summary>Both gzip wrappers honor an explicit choice to close or retain the supplied stream.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StreamDisposal_ExplicitOwnership_ClosesOnlyOwnedInputs(bool leaveOpen)
    {
        var sut = new GZipPayloadTransform(leaveOpen: leaveOpen);
        using var stored = new MemoryStream();
        await using (var encoder = sut.CreateEncodingStream(stored, ReadOnlyMemory<byte>.Empty))
        {
            await encoder.WriteAsync("report"u8.ToArray());
        }

        stored.CanWrite.ShouldBe(leaveOpen);
        using var source = new MemoryStream(stored.ToArray());
        await using (var decoder = sut.CreateDecodingStream(source, ReadOnlyMemory<byte>.Empty))
        {
            await decoder.CopyToAsync(Stream.Null);
        }

        source.CanRead.ShouldBe(leaveOpen);
    }

    /// <summary>Unexpected parameters fail before source consumption or destination writes.</summary>
    [Fact]
    public void Factories_UnexpectedParameters_RejectWithoutConsumingStreams()
    {
        var sut = new GZipPayloadTransform();
        using var source = new MemoryStream([1, 2, 3]);
        using var destination = new MemoryStream();

        Should.Throw<ArgumentException>(() => sut.CreateEncodingStream(destination, new byte[] { 1 }));
        Should.Throw<ArgumentException>(() => sut.CreateDecodingStream(source, new byte[] { 1 }));

        source.Position.ShouldBe(0);
        destination.Length.ShouldBe(0);
    }

    /// <summary>The shared descriptor keeps the existing ordered format/version/parameter JSON shape.</summary>
    [Fact]
    public void Descriptor_ExistingJson_RoundTripsWithoutChangingPersistedFields()
    {
        const string json = "{\"FormatId\":\"gzip\",\"FormatVersion\":1,\"Parameters\":\"\"}";

        var sut = JsonSerializer.Deserialize<StreamingPayloadTransformDescriptor>(json);

        sut.ShouldBe(new StreamingPayloadTransformDescriptor("gzip", 1));
        JsonSerializer.Serialize(sut).ShouldBe(json);
    }
}