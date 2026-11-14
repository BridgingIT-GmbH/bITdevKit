// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Declares codec resource/access requirements rather than inferring them from stream-shaped methods.</summary>
/// <param name="CanReadForwardOnly">Whether decoding needs neither Length nor seeking.</param>
/// <param name="CanWriteForwardOnly">Whether encoding supports a non-seekable destination.</param>
/// <param name="RequiresSeekableInput">Whether input must support seeking before any bytes are consumed.</param>
/// <param name="MaterializesValue">Whether decoding creates the returned object graph.</param>
/// <param name="UsesNativeAsyncIO">Whether I/O is asynchronous rather than a synchronous adapter call.</param>
/// <param name="MaximumScratchBytes">A declared upper scratch bound, or null when the caller must supply admission.</param>
/// <param name="SupportsRawByteSeeking">Whether callers may seek within the encoded representation independently of object decoding.</param>
/// <param name="SupportsRawByteRanges">Whether callers may request checked partial encoded-byte ranges without decoding the object.</param>
/// <example><code>var capabilities = ValueCodecCapabilities.ForwardOnly;</code></example>
public sealed record ValueCodecCapabilities(
    bool CanReadForwardOnly,
    bool CanWriteForwardOnly,
    bool RequiresSeekableInput,
    bool MaterializesValue,
    bool UsesNativeAsyncIO,
    long? MaximumScratchBytes = null,
    bool SupportsRawByteSeeking = false,
    bool SupportsRawByteRanges = false)
{
    /// <summary>Gets the capabilities of a native asynchronous forward-only typed codec.</summary>
    public static ValueCodecCapabilities ForwardOnly { get; } = new(true, true, false, true, true);
}