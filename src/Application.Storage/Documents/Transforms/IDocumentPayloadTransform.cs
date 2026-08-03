// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Defines one reversible transformation in the serialized document payload pipeline.
/// </summary>
/// <remarks>
/// The client invokes transforms in registration order before persistence and in reverse order after reads. Every transform
/// records the metadata needed to reverse its output in its own envelope entry. Implementations must not mutate caller-owned
/// content arrays or use unprefixed persistence metadata keys.
/// </remarks>
/// <example><code>IDocumentPayloadTransform transform = new CompressionDocumentPayloadTransform();</code></example>
public interface IDocumentPayloadTransform
{
    /// <summary>
    /// Gets the stable, case-sensitive identifier persisted in the versioned transform envelope.
    /// </summary>
    /// <remarks>The identifier must remain stable across deployments that need to read previously written documents.</remarks>
    /// <example><code>var identifier = transform.Identifier;</code></example>
    string Identifier { get; }

    /// <summary>Transforms serialized bytes before provider persistence.</summary>
    /// <param name="content">The input bytes owned by the caller.</param>
    /// <param name="metadata">The transform-scoped metadata bag to populate with reversible scalar values.</param>
    /// <param name="cancellationToken">The token used to cancel asynchronous transform work.</param>
    /// <returns>A task-like value containing a new transformed byte array.</returns>
    /// <example><code>var storedBytes = await transform.WriteAsync(logicalBytes, metadata, cancellationToken);</code></example>
    ValueTask<byte[]> WriteAsync(byte[] content, PropertyBag metadata, CancellationToken cancellationToken = default);

    /// <summary>Reverses persisted bytes after stored-content integrity has been verified.</summary>
    /// <param name="content">The transformed input bytes owned by the caller.</param>
    /// <param name="metadata">The transform-scoped metadata persisted by <see cref="WriteAsync" />.</param>
    /// <param name="cancellationToken">The token used to cancel asynchronous transform work.</param>
    /// <returns>A task-like value containing a new byte array for the preceding pipeline stage.</returns>
    /// <example><code>var logicalBytes = await transform.ReadAsync(storedBytes, metadata, cancellationToken);</code></example>
    ValueTask<byte[]> ReadAsync(byte[] content, PropertyBag metadata, CancellationToken cancellationToken = default);
}
