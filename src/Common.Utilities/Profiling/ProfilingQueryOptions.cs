// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Configures retained-history query admission and bounded result selections.</summary>
/// <example><code>var limit = options.Queries.MaximumAnalysisRecords;</code></example>
public sealed class ProfilingQueryOptions
{
    /// <summary>Gets or sets PageSize.</summary>
    /// <example><code>var value = options.PageSize;</code></example>
    public int PageSize { get; set; } = 50;

    /// <summary>Gets or sets MaximumPageSize.</summary>
    /// <example><code>var value = options.MaximumPageSize;</code></example>
    public int MaximumPageSize { get; set; } = 200;

    /// <summary>Gets or sets the maximum contributing-node choices returned with a grouped view.</summary>
    /// <example><code>options.MaximumNodeChoices = 200;</code></example>
    public int MaximumNodeChoices { get; set; } = 200;

    /// <summary>Gets or sets MaximumConcurrentQueries.</summary>
    /// <example><code>var value = options.MaximumConcurrentQueries;</code></example>
    public int MaximumConcurrentQueries { get; set; } = 4;

    /// <summary>Gets or sets MaximumAnalysisRecords.</summary>
    /// <example><code>var value = options.MaximumAnalysisRecords;</code></example>
    public int MaximumAnalysisRecords { get; set; } = 10000;

    /// <summary>Gets or sets MaximumDimensionPredicates.</summary>
    /// <example><code>var value = options.MaximumDimensionPredicates;</code></example>
    public int MaximumDimensionPredicates { get; set; } = 8;

    /// <summary>Gets or sets MaximumGroupingDimensions.</summary>
    /// <example><code>var value = options.MaximumGroupingDimensions;</code></example>
    public int MaximumGroupingDimensions { get; set; } = 4;

    /// <summary>Gets or sets MaximumOverlaySnapshots.</summary>
    /// <example><code>var value = options.MaximumOverlaySnapshots;</code></example>
    public int MaximumOverlaySnapshots { get; set; } = 2000;

    /// <summary>Gets or sets Timeout.</summary>
    /// <example><code>var value = options.Timeout;</code></example>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets BoundaryLifetime.</summary>
    /// <example><code>var value = options.BoundaryLifetime;</code></example>
    public TimeSpan BoundaryLifetime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Validates bounded query settings.</summary>
    /// <example><code>options.Validate();</code></example>
    public void Validate()
    {
        if (this.PageSize <= 0 || this.MaximumPageSize < this.PageSize || this.MaximumConcurrentQueries <= 0 || this.MaximumNodeChoices is <= 0 or > 10000
            || this.MaximumAnalysisRecords <= 0 || this.MaximumDimensionPredicates <= 0 || this.MaximumGroupingDimensions <= 0
            || this.MaximumOverlaySnapshots <= 0 || this.Timeout <= TimeSpan.Zero || this.Timeout == TimeSpan.MaxValue
            || this.BoundaryLifetime <= TimeSpan.Zero || this.BoundaryLifetime == TimeSpan.MaxValue)
        {
            throw new InvalidOperationException("Profiling query limits must be positive and finite.");
        }
    }
}
