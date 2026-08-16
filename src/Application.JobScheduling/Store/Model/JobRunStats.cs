// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using Humanizer;

/// <summary>
/// Represents job run stats.
/// </summary>
public class JobRunStats
{
    /// <summary>
    /// Gets or sets the total runs.
    /// </summary>
    public int TotalRuns { get; set; }

    /// <summary>
    /// Gets or sets the success count.
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Gets or sets the failure count.
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// Gets or sets the interrupt count.
    /// </summary>
    public int InterruptCount { get; set; }

    /// <summary>
    /// Gets or sets the avg run duration ms.
    /// </summary>
    public double AvgRunDurationMs { get; set; }

    /// <summary>
    /// Gets the avg run duration text.
    /// </summary>
    public string AvgRunDurationText => TimeSpan.FromMilliseconds(this.AvgRunDurationMs).Humanize();

    /// <summary>
    /// Gets or sets the max run duration ms.
    /// </summary>
    public long MaxRunDurationMs { get; set; }

    /// <summary>
    /// Gets the max run duration text.
    /// </summary>
    public string MaxRunDurationText => TimeSpan.FromMilliseconds(this.MaxRunDurationMs).Humanize();

    /// <summary>
    /// Gets or sets the min run duration ms.
    /// </summary>
    public long MinRunDurationMs { get; set; }

    /// <summary>
    /// Gets the min run duration text.
    /// </summary>
    public string MinRunDurationText => TimeSpan.FromMilliseconds(this.MinRunDurationMs).Humanize();
}
