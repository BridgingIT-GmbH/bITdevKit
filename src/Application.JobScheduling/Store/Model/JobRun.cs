// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using BridgingIT.DevKit.Common.Converters;
using Humanizer;
using System.Text.Json.Serialization;

public class JobRun
{
    public string Id { get; set; }

    public string JobName { get; set; }

    public string JobGroup { get; set; }

    public string Description { get; set; }

    public string TriggerName { get; set; }

    public string TriggerGroup { get; set; }

    public DateTimeOffset StartTime { get; set; }

    public string StartTimeText => (DateTimeOffset.UtcNow - this.StartTime).Humanize();

    public DateTimeOffset? EndTime { get; set; }

    public string EndTimeText => this.EndTime.HasValue ? (this.EndTime.Value - this.StartTime).Humanize() : string.Empty;

    public DateTimeOffset ScheduledTime { get; set; }

    public long? DurationMs { get; set; }

    public string DurationText => this.DurationMs.HasValue ? TimeSpan.FromMilliseconds(this.DurationMs.Value).Humanize() : null;

    public string Status { get; set; }

    public bool IsRunning => this.Status == "Started";

    public string ErrorMessage { get; set; }

    //[JsonConverter(typeof(DictionaryConverter))]
    public IDictionary<string, object> Data { get; set; }

    public string InstanceName { get; set; }

    public int? Priority { get; set; }

    public string Result { get; set; }

    public int RetryCount { get; set; }

    public string Category { get; set; }
}