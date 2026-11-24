// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Defines independent cache-owned maintenance and refresh scheduling.</summary>
/// <example><code>var options = new CacheBackgroundServiceOptions();</code></example>
public sealed record CacheBackgroundServiceOptions
{
    /// <summary>Gets startup delay before the readiness gate.</summary>
    public TimeSpan StartupDelay { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>Gets bounded initial provider readiness wait.</summary>
    public TimeSpan ReadinessTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets worker stop budget; occupied native work remains accounted.</summary>
    public TimeSpan StopTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets delay after a completed maintenance iteration.</summary>
    public TimeSpan MaintenanceInterval { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>Gets maximum entries per cleanup batch.</summary>
    public int MaintenanceBatchSize { get; init; } = 256;

    /// <summary>Gets maximum cleanup batches per sweep.</summary>
    public int MaximumBatchesPerCollection { get; init; } = 4;

    /// <summary>Gets maximum admission duration per sweep.</summary>
    public TimeSpan NewWorkBudget { get; init; } = TimeSpan.FromSeconds(5);
}