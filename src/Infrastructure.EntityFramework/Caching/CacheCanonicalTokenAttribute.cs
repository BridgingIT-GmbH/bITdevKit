// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

/// <summary>Marks a canonical ASCII cache token for binary storage and comparisons independent of database collation.</summary>
/// <remarks>
/// <see cref="CacheModelConfiguration"/> interprets this annotation with an ASCII string-to-byte-array converter.
/// Apply it only to the provider's normalized hexadecimal identifiers or bounded ASCII authority tokens.
/// User-facing names, keys and dimension text retain their Unicode representation in separate columns.
/// </remarks>
/// <example><code>[CacheCanonicalToken] public string Id { get; set; }</code></example>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CacheCanonicalTokenAttribute : Attribute;