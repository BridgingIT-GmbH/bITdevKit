// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using BridgingIT.DevKit.Common;

/// <summary>A bounded extracted prefix with explicit exhaustion and validation metadata.</summary>
/// <example><code>var value = new AuditExtractionBatch();</code></example>
public sealed record AuditExtractionBatch
{
    /// <summary>Whole selected graphs, never partially loaded records.</summary>
    /// <example><code>var value = snapshot.Items;</code></example>
    public ImmutableArray<AuditExtractionItem> Items { get; init; } = [];

    /// <summary>Position after the fully scanned prefix.</summary>
    /// <example><code>var value = snapshot.Cursor;</code></example>
    public AuditExtractionCursor Cursor { get; init; } = new();

    /// <summary>Whether the fixed committed upper window was scanned completely.</summary>
    /// <example><code>var value = snapshot.Exhausted;</code></example>
    public bool Exhausted { get; init; }
}
