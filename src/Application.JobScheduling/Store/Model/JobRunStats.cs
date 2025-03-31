// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

public class JobRunStats
{
    public int TotalRuns { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public double AvgRunTimeMs { get; set; }
    public long MaxRunTimeMs { get; set; }
    public long MinRunTimeMs { get; set; }
}