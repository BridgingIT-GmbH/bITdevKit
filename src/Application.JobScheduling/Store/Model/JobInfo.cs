// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

public class JobInfo
{
    public string Name { get; set; }
    public string Group { get; set; }
    public string Description { get; set; }
    public string Type { get; set; }
    public string Status { get; set; }
    public int TriggerCount { get; set; }
    public JobRun LastRun { get; set; }
    public JobRunStats LastRunStats { get; set; }
    public string Category { get; set; }
    public IEnumerable<TriggerInfo> Triggers { get; set; }
}
