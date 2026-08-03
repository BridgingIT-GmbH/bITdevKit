// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>Applies reversible gzip compression to serialized document bytes using <see cref="CompressionHelper" />.</summary>
/// <remarks>
/// The transform records its stable identifier in transform-scoped metadata. Stored-content and logical-content integrity
/// are verified by the outer document client before and after decompression respectively.
/// </remarks>
/// <example><code>var transform = new CompressionDocumentPayloadTransform();</code></example>
public sealed class CompressionDocumentPayloadTransform : IDocumentPayloadTransform
{
    /// <inheritdoc />
    public string Identifier => "gzip";
    /// <inheritdoc />
    public async ValueTask<byte[]> WriteAsync(byte[] content, PropertyBag metadata, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        metadata.Set("bdk_compression", this.Identifier);
        return await CompressionHelper.CompressAsync(content);
    }
    /// <inheritdoc />
    public async ValueTask<byte[]> ReadAsync(byte[] content, PropertyBag metadata, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await CompressionHelper.DecompressAsync(content);
    }
}
