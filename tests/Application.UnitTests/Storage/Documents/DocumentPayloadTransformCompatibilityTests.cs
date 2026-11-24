// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Storage.Documents;

using BridgingIT.DevKit.Application.Storage;

/// <summary>Checks the existing gzip document format, metadata and input-ownership contracts.</summary>
public sealed class DocumentPayloadTransformCompatibilityTests
{
    /// <summary>Document compression retains the shared helper's exact gzip framing and does not alter caller bytes.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1024)]
    [InlineData(180000)]
    public async Task WriteAsync_ExistingCompressionFormat_PreservesBytesMetadataAndInput(int length)
    {
        var content = Enumerable.Range(0, length).Select(index => (byte)(index % 251)).ToArray();
        var original = content.ToArray();
        var metadata = new PropertyBag();
        var sut = new CompressionDocumentPayloadTransform();
        var expected = await CompressionHelper.CompressAsync(content);

        var written = await sut.WriteAsync(content, metadata);
        var restored = await sut.ReadAsync(written, metadata);

        sut.Identifier.ShouldBe("gzip");
        metadata.Get<string>("bdk_compression").ShouldBe("gzip");
        written.ShouldBe(expected);
        restored.ShouldBe(original);
        content.ShouldBe(original);
    }

    /// <summary>Null byte arrays retain the existing null result while writes still record the selected format.</summary>
    [Fact]
    public async Task NullContent_WriteAndRead_PreserveExistingResults()
    {
        var metadata = new PropertyBag();
        var sut = new CompressionDocumentPayloadTransform();

        var written = await sut.WriteAsync(null, metadata);
        var restored = await sut.ReadAsync(null, metadata);

        written.ShouldBeNull();
        restored.ShouldBeNull();
        metadata.Get<string>("bdk_compression").ShouldBe("gzip");
    }

    /// <summary>Already canceled operations fail before recording transform metadata or consuming input.</summary>
    [Fact]
    public async Task WriteAsync_AlreadyCanceled_LeavesMetadataAndInputUntouched()
    {
        var metadata = new PropertyBag();
        var content = new byte[] { 1, 2, 3 };
        var sut = new CompressionDocumentPayloadTransform();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() => sut.WriteAsync(content, metadata, cancelled.Token).AsTask());

        metadata.Contains("bdk_compression").ShouldBeFalse();
        content.ShouldBe(new byte[] { 1, 2, 3 });
    }
}