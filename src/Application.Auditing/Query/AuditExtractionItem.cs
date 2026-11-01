// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using BridgingIT.DevKit.Common;

/// <summary>One complete original document at a provider publication position.</summary>
/// <example><code>var value = new AuditExtractionItem();</code></example>
public sealed record AuditExtractionItem
{
    /// <summary>Complete original graph for integrity verification before projection.</summary>
    /// <example><code>var value = snapshot.Record;</code></example>
    public AuditRecord Record { get; init; } = new();

    /// <summary>Committed publication position.</summary>
    /// <example><code>var value = snapshot.Position;</code></example>
    public long Position { get; init; }

    /// <summary>Current provider lifecycle state.</summary>
    /// <example><code>var value = snapshot.StorageState;</code></example>
    public string StorageState { get; init; } = "Active";
}
