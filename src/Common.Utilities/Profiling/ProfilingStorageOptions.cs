// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Bounds provider coordination and background maintenance independently of capture enablement.</summary>
/// <example><code>options.Storage.MaximumWriterLeases = 128;</code></example>
public sealed class ProfilingStorageOptions
{
    /// <summary>Gets or sets maximum live writer leases.</summary>
    /// <example><code>var count = options.MaximumWriterLeases;</code></example>
    public int MaximumWriterLeases { get; set; } = 128;

    /// <summary>Gets or sets maximum unfinished or still-needed clear fences.</summary>
    /// <example><code>var count = options.MaximumClearFences;</code></example>
    public int MaximumClearFences { get; set; } = 64;

    /// <summary>Gets or sets the provider-authoritative preparation deadline.</summary>
    /// <example><code>var duration = options.ClearPreparationTimeout;</code></example>
    public TimeSpan ClearPreparationTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets or sets periodic retention and recovery scheduling.</summary>
    /// <example><code>var interval = options.MaintenanceInterval;</code></example>
    public TimeSpan MaintenanceInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets maximum root graphs removed in a maintenance call.</summary>
    /// <example><code>var count = options.MaximumMaintenanceRoots;</code></example>
    public int MaximumMaintenanceRoots { get; set; } = 512;

    /// <summary>Gets or sets the budget for beginning more maintenance batches.</summary>
    /// <example><code>var budget = options.MaintenanceTimeBudget;</code></example>
    public TimeSpan MaintenanceTimeBudget { get; set; } = TimeSpan.FromMilliseconds(250);

    internal ProfilingStorageOptions Snapshot() => (ProfilingStorageOptions)this.MemberwiseClone();

    /// <summary>Validates finite bounded provider settings.</summary>
    /// <example><code>options.Validate();</code></example>
    public void Validate()
    {
        if (this.MaximumWriterLeases <= 0 || this.MaximumClearFences <= 0 || this.MaximumMaintenanceRoots <= 0
            || this.ClearPreparationTimeout <= TimeSpan.Zero || this.ClearPreparationTimeout == TimeSpan.MaxValue
            || this.MaintenanceInterval <= TimeSpan.Zero || this.MaintenanceInterval == TimeSpan.MaxValue
            || this.MaintenanceTimeBudget <= TimeSpan.Zero || this.MaintenanceTimeBudget == TimeSpan.MaxValue)
        {
            throw new InvalidOperationException("Profiling provider coordination and maintenance limits must be positive and finite.");
        }
    }
}
