// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

using BridgingIT.DevKit.Application.Caching;

/// <summary>Preserves a provider-neutral cache failure code across native relational operations.</summary>
/// <param name="code">The stable failure category returned to cache callers.</param>
/// <param name="message">The bounded diagnostic message without payload or key content.</param>
/// <example><code>throw new CacheRelationalException(CacheErrorCode.Corruption, "Stored metadata is invalid.");</code></example>
public sealed class CacheRelationalException(CacheErrorCode code, string message) : Exception(message)
{
    /// <summary>Gets the provider-neutral cache failure category.</summary>
    public CacheErrorCode Code { get; } = code;
}