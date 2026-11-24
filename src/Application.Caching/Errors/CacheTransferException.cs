// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Classifies a native or logical streaming failure after a stream has already been acquired.</summary>
/// <remarks>The fixed message contains no keys, payloads, connection details or source exception text. The read handle separately reports terminal completion.</remarks>
/// <example><code>throw new CacheTransferException(CacheErrorCode.Corruption);</code></example>
public sealed class CacheTransferException(CacheErrorCode code) : IOException("The bounded cache transfer could not complete.")
{
    /// <summary>Gets the explicit transfer failure class used by completion and transport adapters.</summary>
    public CacheErrorCode Code { get; } = Enum.IsDefined(code) ? code : throw new ArgumentOutOfRangeException(nameof(code));
}