// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using BridgingIT.DevKit.Common.Converters;
using Humanizer;
using System.Text.Json.Serialization;

/// <summary>
/// Represents job run.
/// </summary>
public class JobRun
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the job name.
    /// </summary>
    public string JobName { get; set; }

    /// <summary>
    /// Gets or sets the job group.
    /// </summary>
    public string JobGroup { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the trigger name.
    /// </summary>
    public string TriggerName { get; set; }

    /// <summary>
    /// Gets or sets the trigger group.
    /// </summary>
    public string TriggerGroup { get; set; }

    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Gets the start time text.
    /// </summary>
    public string StartTimeText => (DateTimeOffset.UtcNow - this.StartTime).Humanize();

    /// <summary>
    /// Gets or sets the end time.
    /// </summary>
    public DateTimeOffset? EndTime { get; set; }

    /// <summary>
    /// Gets the end time text.
    /// </summary>
    public string EndTimeText => this.EndTime.HasValue ? (this.EndTime.Value - this.StartTime).Humanize() : string.Empty;

    /// <summary>
    /// Gets or sets the scheduled time.
    /// </summary>
    public DateTimeOffset ScheduledTime { get; set; }

    /// <summary>
    /// Gets or sets the duration ms.
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// Gets the duration text.
    /// </summary>
    public string DurationText => this.DurationMs.HasValue ? TimeSpan.FromMilliseconds(this.DurationMs.Value).Humanize() : null;

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets the is running.
    /// </summary>
    public bool IsRunning => this.Status == "Started";

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string ErrorMessage { get; set; }

    //[JsonConverter(typeof(DictionaryConverter))]
    /// <summary>
    /// Gets or sets the persisted job data values.
    /// </summary>
    public IDictionary<string, object> Data { get; set; }

    /// <summary>
    /// Gets or sets the instance name.
    /// </summary>
    public string InstanceName { get; set; }

    /// <summary>
    /// Gets or sets the priority.
    /// </summary>
    public int? Priority { get; set; }

    /// <summary>
    /// Gets or sets the result.
    /// </summary>
    public string Result { get; set; }

    /// <summary>
    /// Gets or sets the retry count.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Gets or sets the category.
    /// </summary>
    public string Category { get; set; }
}
