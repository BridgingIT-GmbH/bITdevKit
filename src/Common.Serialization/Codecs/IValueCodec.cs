// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Encodes a declared value contract without imposing storage or application policy.</summary>
/// <typeparam name="T">The caller's value contract.</typeparam>
/// <example><code>var result = await codec.EncodeAsync(value, destination, cancellationToken);</code></example>
public interface IValueCodec<T>
{
    /// <summary>Gets the stable byte-format identifier, independent of CLR type names.</summary>
    string FormatId { get; }

    /// <summary>Gets the positive byte-format version.</summary>
    int FormatVersion { get; }

    /// <summary>Gets the actual access and materialization requirements.</summary>
    ValueCodecCapabilities Capabilities { get; }

    /// <summary>Writes a value, leaving the caller's destination open. Cancellation propagates.</summary>
    ValueTask<Result> EncodeAsync(T value, Stream destination, CancellationToken cancellationToken = default);

    /// <summary>Reads a complete value, leaving the caller's source open. Cancellation propagates.</summary>
    ValueTask<Result<T>> DecodeAsync(Stream source, CancellationToken cancellationToken = default);
}