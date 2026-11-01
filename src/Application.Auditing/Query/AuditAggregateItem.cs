// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using BridgingIT.DevKit.Common;

/// <summary>An exact provider-side scoped group count.</summary>
/// <example><code>var value = new AuditAggregateItem();</code></example>
public sealed record AuditAggregateItem
{
    /// <summary>Invariant group label, null for absent values.</summary>
    /// <example><code>var value = snapshot.Key;</code></example>
    public string Key { get; init; }

    /// <summary>Exact matching record count.</summary>
    /// <example><code>var value = snapshot.Count;</code></example>
    public long Count { get; init; }
}
