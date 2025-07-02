// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using Humanizer;

public class JobRunStats
{
    public int TotalRuns { get; set; }

    public int SuccessCount { get; set; }

    public int FailureCount { get; set; }

    public int InterruptCount { get; set; }

    public double AvgRunDurationMs { get; set; }

    public string AvgRunDurationText => TimeSpan.FromMilliseconds(this.AvgRunDurationMs).Humanize();

    public long MaxRunDurationMs { get; set; }

    public string MaxRunDurationText => TimeSpan.FromMilliseconds(this.MaxRunDurationMs).Humanize();

    public long MinRunDurationMs { get; set; }

    public string MinRunDurationText => TimeSpan.FromMilliseconds(this.MinRunDurationMs).Humanize();
}