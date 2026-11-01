// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

/// <summary>Shared age-based archive/remove policy; automatic maintenance is off until host configured.</summary>
/// <example><code>var retention = new AuditRetentionOptions { ArchiveAfter = TimeSpan.FromDays(30) };</code></example>
public sealed record AuditRetentionOptions
{
    /// <summary>Delay after application startup before the first maintenance sweep.</summary>
    /// <example><code>var delay = options.StartupDelay;</code></example>
    public TimeSpan StartupDelay { get; init; } = TimeSpan.Zero;

    /// <summary>Total age since CompletedUtc before archiving, or null to disable.</summary>
    /// <example><code>var age = options.ArchiveAfter;</code></example>
    public TimeSpan? ArchiveAfter { get; init; }

    /// <summary>Total age since CompletedUtc before removal of already archived graphs.</summary>
    /// <example><code>var age = options.RemoveAfter;</code></example>
    public TimeSpan? RemoveAfter { get; init; }

    /// <summary>Finite delay between automatic maintenance attempts.</summary>
    /// <example><code>var delay = options.Interval;</code></example>
    public TimeSpan Interval { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Maximum complete graphs per provider mutation.</summary>
    /// <example><code>var batch = options.BatchSize;</code></example>
    public int BatchSize { get; init; } = 100;

    /// <summary>Maximum archive and removal batches per sweep.</summary>
    /// <example><code>var bound = options.MaxBatches;</code></example>
    public int MaxBatches { get; init; } = 10;

    /// <summary>Rejects unbounded or inconsistent age/worker settings without reading storage.</summary>
    /// <example><code>options.Validate();</code></example>
    public void Validate()
    {
        if (this.StartupDelay < TimeSpan.Zero || this.StartupDelay > TimeSpan.FromHours(1)) throw new ArgumentException("Audit retention startup delay is invalid.");
        if (this.ArchiveAfter < TimeSpan.Zero || this.RemoveAfter < TimeSpan.Zero || this.RemoveAfter.HasValue && !this.ArchiveAfter.HasValue ||
            this.RemoveAfter.HasValue && this.RemoveAfter <= this.ArchiveAfter || this.Interval < TimeSpan.FromMilliseconds(50) || this.Interval > TimeSpan.FromDays(1) ||
            this.BatchSize is < 1 or > 1_000 || this.MaxBatches is < 1 or > 100)
            throw new ArgumentException("Audit retention configuration is invalid.");
    }
}
