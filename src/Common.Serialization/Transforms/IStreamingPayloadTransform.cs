// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Defines one versioned, reversible streaming transformation independently of storage and application policy.</summary>
/// <remarks>Factories create per-transfer streams and must not buffer the complete value. Implementations may be shared concurrently.
/// Callers account the declared state before opening a stream, bound input and output separately, and approve persisted formats before decoding.
/// The implementation defines source ownership and parameter validation. Dispose encoders before consuming their final output.</remarks>
/// <example><code>IStreamingPayloadTransform transform = new GZipPayloadTransform();
/// await using var encoder = transform.CreateEncodingStream(destination, ReadOnlyMemory&lt;byte&gt;.Empty);
/// await source.CopyToAsync(encoder, cancellationToken);</code></example>
public interface IStreamingPayloadTransform
{
    /// <summary>Gets the stable persisted format identifier, independent of aliases and CLR type names.</summary>
    /// <example><code>var format = transform.FormatId;</code></example>
    string FormatId { get; }

    /// <summary>Gets the positive persisted format version supported by this implementation.</summary>
    /// <example><code>var version = transform.FormatVersion;</code></example>
    int FormatVersion { get; }

    /// <summary>Gets a finite conservative per-stream state and workspace estimate, excluding separately owned input and output.</summary>
    /// <remarks>This is an admission-accounting estimate, not a hard limit on managed or native memory.</remarks>
    /// <example><code>var workspaceBytes = transform.MaximumStateBytes;</code></example>
    long MaximumStateBytes { get; }

    /// <summary>Creates a forward-only encoder writing transformed bytes to the supplied destination.</summary>
    /// <param name="destination">The destination receiving the transformed representation.</param>
    /// <param name="parameters">Caller-owned parameters which must be validated and copied before being retained.</param>
    /// <returns>The per-transfer encoding stream; dispose it to finish the representation.</returns>
    /// <example><code>await using var encoder = transform.CreateEncodingStream(destination, ReadOnlyMemory&lt;byte&gt;.Empty);</code></example>
    Stream CreateEncodingStream(Stream destination, ReadOnlyMemory<byte> parameters);

    /// <summary>Creates a forward-only decoder reading a previously approved representation.</summary>
    /// <param name="source">The source containing transformed bytes.</param>
    /// <param name="parameters">Previously approved persisted parameters; invalid parameters must fail before logical bytes are returned.</param>
    /// <returns>The per-transfer decoding stream.</returns>
    /// <example><code>await using var decoder = transform.CreateDecodingStream(source, ReadOnlyMemory&lt;byte&gt;.Empty);</code></example>
    Stream CreateDecodingStream(Stream source, ReadOnlyMemory<byte> parameters);
}

/// <summary>Describes one transform in encoding order; decoding applies an ordered stack in reverse.</summary>
/// <param name="FormatId">The stable persisted format identifier.</param>
/// <param name="FormatVersion">The positive format version.</param>
/// <param name="Parameters">Opaque encoded parameters whose encoding and finite bounds are defined by the consuming format.</param>
/// <remarks>This data descriptor does not activate implementations or validate a storage format.</remarks>
/// <example><code>var descriptor = new StreamingPayloadTransformDescriptor("gzip", 1);</code></example>
public record StreamingPayloadTransformDescriptor(string FormatId, int FormatVersion, string Parameters = "");