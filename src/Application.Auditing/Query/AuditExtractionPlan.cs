// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using BridgingIT.DevKit.Common;

/// <summary>A validated selection and optional confirmed persistence checkpoint.</summary>
/// <example><code>var value = new AuditExtractionPlan();</code></example>
public sealed record AuditExtractionPlan
{
    /// <summary>Fixed absolute selection, scope and exposure fingerprint.</summary>
    /// <example><code>var value = snapshot.Selection;</code></example>
    public AuditSearchPlan Selection { get; init; } = new();

    /// <summary>Expected source store identity when resuming.</summary>
    /// <example><code>var value = snapshot.StoreId;</code></example>
    public Guid? StoreId { get; init; }

    /// <summary>Expected persistence-position epoch when resuming.</summary>
    /// <example><code>var value = snapshot.StoreEpoch;</code></example>
    public Guid? StoreEpoch { get; init; }

    /// <summary>Last client-confirmed fully scanned persistence boundary.</summary>
    /// <example><code>var value = snapshot.AfterPosition;</code></example>
    public long AfterPosition { get; init; } = 0;

    /// <summary>Prior confirmed removal-availability baseline.</summary>
    /// <example><code>var value = snapshot.AvailabilityBaseline;</code></example>
    public long? AvailabilityBaseline { get; init; }

    /// <summary>Bounded provider batch size.</summary>
    /// <example><code>var value = snapshot.BatchSize;</code></example>
    public int BatchSize { get; init; } = 100;

    /// <summary>Maximum retained original-document bytes returned by one provider batch.</summary>
    /// <example><code>var budget = cursor.MaxBatchBytes;</code></example>
    public int MaxBatchBytes { get; init; } = 4_194_304;

    /// <summary>Maximum permitted complete original document for this extraction operation.</summary>
    /// <example><code>var maximum = cursor.MaxRecordBytes;</code></example>
    public int MaxRecordBytes { get; init; } = 16_777_216;
}
