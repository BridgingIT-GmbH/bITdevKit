// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Provides common maintenance-job options.
/// </summary>
public abstract record JobMaintenanceOptions
{
    public bool DryRun { get; init; }

    public int BatchSize { get; init; } = 100;
}

/// <summary>
/// Configures completed-occurrence archive behavior.
/// </summary>
public sealed record JobArchiveOccurrencesJobData : JobMaintenanceOptions
{
    public TimeSpan RetentionWindow { get; init; } = TimeSpan.FromDays(30);

    public IReadOnlyList<JobOccurrenceStatus> Statuses { get; init; } =
    [
        JobOccurrenceStatus.Completed,
        JobOccurrenceStatus.Failed,
        JobOccurrenceStatus.Cancelled,
    ];

    public string JobName { get; init; }

    public string TriggerName { get; init; }
}

/// <summary>
/// Configures archived execution-history purge behavior.
/// </summary>
public sealed record JobPurgeHistoryJobData : JobMaintenanceOptions
{
    public TimeSpan RetentionWindow { get; init; } = TimeSpan.FromDays(30);
}

/// <summary>
/// Configures retained occurrence purge behavior.
/// </summary>
public sealed record JobPurgeOccurrencesRequest : JobMaintenanceOptions
{
    public DateTimeOffset? OlderThan { get; init; }

    public IReadOnlyList<JobOccurrenceStatus> Statuses { get; init; }

    public string JobName { get; init; }

    public string TriggerName { get; init; }

    public bool? IsArchived { get; init; }
}

/// <summary>
/// Configures expired-lease release behavior.
/// </summary>
public sealed record JobReleaseExpiredLeasesJobData : JobMaintenanceOptions;

/// <summary>
/// Configures stuck-occurrence recovery behavior.
/// </summary>
public sealed record JobRecoverStuckOccurrencesJobData : JobMaintenanceOptions
{
    public TimeSpan StuckFor { get; init; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// Configures orphaned runtime-state detection behavior.
/// </summary>
public sealed record JobDetectOrphanedRuntimeStateJobData : JobMaintenanceOptions;

/// <summary>
/// Describes one maintenance execution result.
/// </summary>
public sealed record JobMaintenanceReport(
    string Operation,
    bool DryRun,
    int MatchedCount,
    int ProcessedCount,
    int RemainingCount,
    IReadOnlyList<string> AffectedIds,
    IReadOnlyList<string> Diagnostics);