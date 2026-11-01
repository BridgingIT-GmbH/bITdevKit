// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using BridgingIT.DevKit.Common;

/// <summary>A fixed committed upper window independent of event timestamps and queue admissions.</summary>
/// <example><code>var value = new AuditExtractionCursor();</code></example>
public sealed record AuditExtractionCursor
{
    /// <summary>Provider source identity.</summary>
    /// <example><code>var value = snapshot.StoreId;</code></example>
    public Guid StoreId { get; init; }

    /// <summary>Provider persistence-position epoch.</summary>
    /// <example><code>var value = snapshot.StoreEpoch;</code></example>
    public Guid StoreEpoch { get; init; }

    /// <summary>Validated absolute effective selection.</summary>
    /// <example><code>var value = snapshot.Selection;</code></example>
    public AuditSearchPlan Selection { get; init; } = new();

    /// <summary>Last fully emitted/scanned position within this window.</summary>
    /// <example><code>var value = snapshot.AfterPosition;</code></example>
    public long AfterPosition { get; init; }

    /// <summary>Optional original chunk lower boundary retained through final output validation, so removing already emitted evidence invalidates publication.</summary>
    /// <example><code>var validation = cursor with { RequiredAfterPosition = windowStart };</code></example>
    public long? RequiredAfterPosition { get; init; }

    /// <summary>Fixed committed upper boundary.</summary>
    /// <example><code>var value = snapshot.UpperPosition;</code></example>
    public long UpperPosition { get; init; }

    /// <summary>Membership generation at window open.</summary>
    /// <example><code>var value = snapshot.LifecycleGeneration;</code></example>
    public long LifecycleGeneration { get; init; }

    /// <summary>Removal-availability boundary at window open.</summary>
    /// <example><code>var value = snapshot.AvailabilityBaseline;</code></example>
    public long AvailabilityBaseline { get; init; }

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
