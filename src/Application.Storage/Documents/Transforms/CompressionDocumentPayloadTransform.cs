// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>Applies reversible gzip compression to serialized document bytes using the shared streaming transform.</summary>
/// <remarks>
/// The transform records its stable identifier in transform-scoped metadata. Stored-content and logical-content integrity
/// are verified by the outer document client before and after decompression respectively.
/// </remarks>
/// <example><code>var transform = new CompressionDocumentPayloadTransform();</code></example>
public sealed class CompressionDocumentPayloadTransform : IDocumentPayloadTransform
{
    private static readonly IStreamingPayloadTransform Compression = new GZipPayloadTransform();

    /// <inheritdoc />
    public string Identifier => "gzip";

    /// <inheritdoc />
    public async ValueTask<byte[]> WriteAsync(byte[] content, PropertyBag metadata, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        metadata.Set("bdk_compression", this.Identifier);
        if (content is null) { return null; }

        using var source = new MemoryStream(content);
        using var destination = new MemoryStream();
        await using (var encoder = Compression.CreateEncodingStream(destination, ReadOnlyMemory<byte>.Empty))
        {
            await source.CopyToAsync(encoder);
            await encoder.FlushAsync();
        }

        return destination.ToArray();
    }

    /// <inheritdoc />
    public async ValueTask<byte[]> ReadAsync(byte[] content, PropertyBag metadata, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (content is null) { return null; }

        using var source = new MemoryStream(content);
        using var destination = new MemoryStream();
        await using (var decoder = Compression.CreateDecodingStream(source, ReadOnlyMemory<byte>.Empty))
        {
            await decoder.CopyToAsync(destination);
            await destination.FlushAsync();
        }

        return destination.ToArray();
    }
}