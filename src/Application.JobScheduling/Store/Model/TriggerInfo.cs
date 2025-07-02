// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using Humanizer;

public class TriggerInfo
{
    public string Name { get; set; }

    public string Group { get; set; }

    public string Description { get; set; }

    public string CronExpression { get; set; }

    public DateTimeOffset? NextFireTime { get; set; }

    public string NextFireTimeText => this.NextFireTime.HasValue ? (this.NextFireTime.Value - DateTimeOffset.UtcNow).Humanize() : string.Empty;

    public DateTimeOffset? PreviousFireTime { get; set; }

    public string PreviousFireTimeText => this.PreviousFireTime.HasValue ? (DateTimeOffset.UtcNow - this.PreviousFireTime.Value).Humanize() : string.Empty;

    public string State { get; set; }
}