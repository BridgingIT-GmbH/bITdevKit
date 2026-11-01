// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;

/// <summary>Copied optional source change evidence; it does not infer a database commit.</summary>
/// <example><code>var value = new AuditChangeEvidence();</code></example>
public sealed record AuditChangeEvidence
{
    /// <summary>Stable evidence identity.</summary>
    /// <example><code>var value = snapshot.Id;</code></example>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Associated action occurrence.</summary>
    /// <example><code>var value = snapshot.ActionId;</code></example>
    public Guid? ActionId { get; init; }

    /// <summary>Approved source store identity.</summary>
    /// <example><code>var value = snapshot.SourceStoreKey;</code></example>
    public string SourceStoreKey { get; init; }

    /// <summary>Original source change-set identity.</summary>
    /// <example><code>var value = snapshot.ChangeSetId;</code></example>
    public string ChangeSetId { get; init; }

    /// <summary>Original source row identities.</summary>
    /// <example><code>var value = snapshot.SourceRowIds;</code></example>
    public ImmutableArray<string> SourceRowIds { get; init; } = [];

    /// <summary>Optional source bulk-operation identity.</summary>
    /// <example><code>var value = snapshot.BulkOperationId;</code></example>
    public string BulkOperationId { get; init; }

    /// <summary>Stable resource type alias.</summary>
    /// <example><code>var value = snapshot.EntityType;</code></example>
    public string EntityType { get; init; }

    /// <summary>Exact source entity identity.</summary>
    /// <example><code>var value = snapshot.EntityId;</code></example>
    public string EntityId { get; init; }

    /// <summary>Observed mutation operation.</summary>
    /// <example><code>var value = snapshot.Operation;</code></example>
    public string Operation { get; init; }

    /// <summary>Source capture strategy.</summary>
    /// <example><code>var value = snapshot.CaptureSource;</code></example>
    public string CaptureSource { get; init; }

    /// <summary>Source capture strategy, independent of the audit copy policy.</summary>
    /// <example><code>var strategy = evidence.CaptureStrategy;</code></example>
    public string CaptureStrategy { get; init; }

    /// <summary>Source capture status, including summary, skipped, or failed capture.</summary>
    /// <example><code>var status = evidence.CaptureStatus;</code></example>
    public string CaptureStatus { get; init; }

    /// <summary>Snapshot or ReferenceOnly audit copying policy.</summary>
    /// <example><code>var mode = evidence.CopyMode;</code></example>
    public string CopyMode { get; init; }

    /// <summary>Source-reported affected count, when available for bulk summaries.</summary>
    /// <example><code>var count = evidence.AffectedEntityCount;</code></example>
    public int? AffectedEntityCount { get; init; }

    /// <summary>Committed, RolledBack, or Unknown source transaction evidence.</summary>
    /// <example><code>var value = snapshot.PersistenceState;</code></example>
    public string PersistenceState { get; init; } = "Unknown";

    /// <summary>Observed evidence time.</summary>
    /// <example><code>var value = snapshot.Timestamp;</code></example>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>Permitted property deltas, empty for Unknown or RolledBack by default.</summary>
    /// <example><code>var value = snapshot.Properties;</code></example>
    public ImmutableArray<AuditPropertyChange> Properties { get; init; } = [];

}
