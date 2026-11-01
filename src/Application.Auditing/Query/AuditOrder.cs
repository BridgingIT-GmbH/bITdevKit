// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using BridgingIT.DevKit.Common;

/// <summary>A catalog-approved deterministic ordering.</summary>
/// <example><code>var value = new AuditOrder();</code></example>
public sealed record AuditOrder
{
    /// <summary>Approved root scalar field.</summary>
    /// <example><code>var value = snapshot.Field;</code></example>
    public string Field { get; init; } = "StartedUtc";

    /// <summary>Requested sort direction.</summary>
    /// <example><code>var value = snapshot.Direction;</code></example>
    public OrderDirection Direction { get; init; } = OrderDirection.Descending;
}
