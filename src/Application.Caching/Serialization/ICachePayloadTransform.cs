// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Defines a code-approved, versioned, streaming persisted transformation. Factories must honor forward-only access and finite state charges.</summary>
/// <remarks>Encoding streams write to the supplied destination; decoding streams read from the supplied source. Disposing a wrapper may close that supplied view.
/// Implementations are shared between calls and must be thread safe; each returned stream owns only its per-transfer state. No whole-value buffering is permitted.
/// The inherited streaming contract forwards to these cache members, including explicit interface implementations.</remarks>
/// <example><code>caching.WithPayloadTransform("authenticated", services => new AuthenticatedTransform(services));</code></example>
public interface ICachePayloadTransform : IStreamingPayloadTransform
{
    /// <inheritdoc cref="IStreamingPayloadTransform.FormatId" />
    new string FormatId { get; }

    /// <inheritdoc cref="IStreamingPayloadTransform.FormatVersion" />
    new int FormatVersion { get; }

    /// <inheritdoc cref="IStreamingPayloadTransform.MaximumStateBytes" />
    new long MaximumStateBytes { get; }

    /// <summary>Creates a forward-only encoder over a selected-store staging destination with parameters bounded to 256 bytes.</summary>
    /// <inheritdoc cref="IStreamingPayloadTransform.CreateEncodingStream" />
    new Stream CreateEncodingStream(Stream destination, ReadOnlyMemory<byte> parameters);

    /// <summary>Creates a forward-only decoder for an approved format with parameters bounded to 256 bytes.</summary>
    /// <inheritdoc cref="IStreamingPayloadTransform.CreateDecodingStream" />
    new Stream CreateDecodingStream(Stream source, ReadOnlyMemory<byte> parameters);

    /// <inheritdoc />
    string IStreamingPayloadTransform.FormatId => this.FormatId;

    /// <inheritdoc />
    int IStreamingPayloadTransform.FormatVersion => this.FormatVersion;

    /// <inheritdoc />
    long IStreamingPayloadTransform.MaximumStateBytes => this.MaximumStateBytes;

    /// <inheritdoc />
    Stream IStreamingPayloadTransform.CreateEncodingStream(Stream destination, ReadOnlyMemory<byte> parameters) => this.CreateEncodingStream(destination, parameters);

    /// <inheritdoc />
    Stream IStreamingPayloadTransform.CreateDecodingStream(Stream source, ReadOnlyMemory<byte> parameters) => this.CreateDecodingStream(source, parameters);
}

/// <summary>Describes one transformation in encode order. Decoding reverses this list.</summary>
/// <param name="FormatId">Stable trusted format ID.</param>
/// <param name="FormatVersion">Positive format version.</param>
/// <param name="Parameters">At most 256 bytes encoded as canonical Base64; not a CLR type or configuration instruction.</param>
/// <example><code>var format = new CachePayloadTransformDescriptor("authenticated", 1, "");</code></example>
public sealed record CachePayloadTransformDescriptor(string FormatId, int FormatVersion, string Parameters = "")
    : StreamingPayloadTransformDescriptor(FormatId, FormatVersion, Parameters);