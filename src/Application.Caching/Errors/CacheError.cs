// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Identifies a recoverable cache failure without conflating misses, source errors, or provider outages.</summary>
/// <param name="code">The stable error code.</param>
/// <param name="message">A non-sensitive explanation.</param>
/// <example><code>var error = new CacheError(CacheErrorCode.Conflict, "The stored version changed.");</code></example>
public sealed class CacheError(CacheErrorCode code, string message) : ResultErrorBase(message)
{
    /// <summary>Gets the failure classification.</summary>
    public CacheErrorCode Code { get; } = code;
}

/// <summary>Defines cache failure classes that callers and transport adapters can distinguish.</summary>
public enum CacheErrorCode
{
    /// <summary>Input or policy violates its declared bounds.</summary>
    Validation,

    /// <summary>The collection or value contract is unknown.</summary>
    Contract,

    /// <summary>The selected capability is unavailable.</summary>
    Unsupported,

    /// <summary>The provider or ownership authority is unavailable.</summary>
    ProviderUnavailable,

    /// <summary>A version, owner, or invalidation fence changed.</summary>
    Conflict,

    /// <summary>Finite admission or storage capacity was exhausted.</summary>
    Capacity,

    /// <summary>A caller exhausted its coordination wait budget.</summary>
    CoordinationTimeout,

    /// <summary>The owned source execution exceeded its total deadline.</summary>
    SourceTimeout,

    /// <summary>Source loading failed without producing a cacheable value.</summary>
    SourceFailure,

    /// <summary>A configured source policy rejected execution.</summary>
    SourceRejected,

    /// <summary>A loader attempted nested source work or refresh.</summary>
    NestedCacheLoad,

    /// <summary>Persisted content or metadata failed integrity validation.</summary>
    Corruption,

    /// <summary>A persisted representation has no registered compatible reader.</summary>
    Compatibility,

    /// <summary>A mutation may have committed but acknowledgement is uncertain.</summary>
    Indeterminate,

    /// <summary>A bounded selection or maintenance unit did not complete its scope.</summary>
    Incomplete,

    /// <summary>A requested entry does not exist.</summary>
    NotFound
}