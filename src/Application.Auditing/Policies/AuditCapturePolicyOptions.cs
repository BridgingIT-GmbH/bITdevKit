// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;

/// <summary>Explicit field-name policies applied uniformly to every captured detail.</summary>
/// <example><code>var policy = new AuditCapturePolicyOptions { RedactNames = ["privateNote"] };</code></example>
public sealed record AuditCapturePolicyOptions
{
    /// <summary>Names whose values are excluded. Exclusion has highest precedence.</summary>
    /// <example><code>var policy = options with { ExcludeNames = options.ExcludeNames.Add("privateNote") };</code></example>
    public ImmutableHashSet<string> ExcludeNames { get; init; } = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase,
        "password", "secret", "token", "authorization", "cookie", "access_token", "refresh_token", "email", "subject", "body", "content", "connectionString");

    /// <summary>Additional names replaced with an explicit redaction marker.</summary>
    /// <example><code>var policy = options with { RedactNames = ImmutableHashSet.Create("privateNote") };</code></example>
    public ImmutableHashSet<string> RedactNames { get; init; } = ImmutableHashSet<string>.Empty;

    /// <summary>Additional names retained only as SHA-256 projected-value digests; hashing does not establish anonymity.</summary>
    /// <example><code>var policy = options with { HashNames = ImmutableHashSet.Create("externalReference") };</code></example>
    public ImmutableHashSet<string> HashNames { get; init; } = ImmutableHashSet<string>.Empty;
}
