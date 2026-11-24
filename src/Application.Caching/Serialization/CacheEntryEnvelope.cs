// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Identifies an explicit persisted payload format independently of provider metadata and CLR type names.</summary>
/// <example><code>var version = envelope.EnvelopeVersion;</code></example>
public sealed record CacheEntryEnvelope
{
    /// <summary>Gets the persisted envelope protocol version.</summary>
    public int EnvelopeVersion { get; init; } = 1;

    /// <summary>Gets the checked canonical identity digest; the provider also compares the full stored identity.</summary>
    public string IdentityDigest { get; init; }

    /// <summary>Gets the store-issued immutable generation token.</summary>
    public string EntryVersion { get; init; }

    /// <summary>Gets the stable registered value-contract alias.</summary>
    public string ContractId { get; init; }

    /// <summary>Gets the registered logical codec/raw-format identifier.</summary>
    public string CodecId { get; init; }

    /// <summary>Gets the registered codec/raw-format version.</summary>
    public int CodecVersion { get; init; } = 1;

    /// <summary>Gets the explicit none, gzip, or brotli transformation identifier.</summary>
    public string Compression { get; init; } = "none";

    /// <summary>Gets at most eight explicit transformations in encode order for envelope version two. Version one keeps its original compression format.</summary>
    public IReadOnlyList<CachePayloadTransformDescriptor> Transforms { get; init; } = [];

    /// <summary>Gets the format-one independently checked payload frame capacity.</summary>
    public int BlockSize { get; init; } = 65536;
}

/// <summary>Stores terminal logical and transformed-body sizes and SHA-256 digests inside the persisted envelope.</summary>
/// <param name="LogicalBytes">The original logical representation size.</param>
/// <param name="BodyBytes">The transformed payload size before envelope framing.</param>
/// <param name="LogicalHash">The original logical-byte SHA-256 digest.</param>
/// <param name="BodyHash">The transformed body SHA-256 digest.</param>
public sealed record CacheEntryEnvelopeFooter(long LogicalBytes, long BodyBytes, string LogicalHash, string BodyHash);

/// <summary>Reports finalized logical, transformed, and complete stored representation measurements.</summary>
/// <param name="LogicalBytes">The original logical representation size.</param>
/// <param name="BodyBytes">The transformed payload size before envelope framing.</param>
/// <param name="StoredBytes">The complete envelope size including header, frames, and footer.</param>
/// <param name="LogicalHash">The original logical-byte SHA-256 digest.</param>
/// <param name="StoredHash">The SHA-256 digest of all persisted envelope bytes.</param>
/// <param name="Compression">The persisted transformation identifier.</param>
public readonly record struct CacheEncodedPayload(long LogicalBytes, long BodyBytes, long StoredBytes,
    string LogicalHash, string StoredHash, string Compression);