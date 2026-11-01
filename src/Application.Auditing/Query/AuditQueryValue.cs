// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using BridgingIT.DevKit.Common;

/// <summary>A validated scalar or bounded set; provider plans retain no mutable client objects.</summary>
/// <example><code>var value = new AuditQueryValue();</code></example>
public sealed record AuditQueryValue
{
    /// <summary>String, Integer, Decimal, Boolean, Timestamp, or Null.</summary>
    /// <example><code>var value = snapshot.Kind;</code></example>
    public string Kind { get; init; } = "Null";

    /// <summary>Invariant text or exact opaque identity.</summary>
    /// <example><code>var value = snapshot.Text;</code></example>
    public string Text { get; init; }

    /// <summary>Exact approved numeric value.</summary>
    /// <example><code>var value = snapshot.Number;</code></example>
    public decimal? Number { get; init; }

    /// <summary>UTC timestamp ticks or integer value.</summary>
    /// <example><code>var value = snapshot.Ticks;</code></example>
    public long? Ticks { get; init; }

    /// <summary>Boolean comparison value.</summary>
    /// <example><code>var value = snapshot.Boolean;</code></example>
    public bool? Boolean { get; init; }

    /// <summary>Validated set membership values.</summary>
    /// <example><code>var value = snapshot.Values;</code></example>
    public ImmutableArray<AuditQueryValue> Values { get; init; } = [];
}
