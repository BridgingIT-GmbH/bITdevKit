// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using Humanizer;

/// <summary>
/// Represents trigger info.
/// </summary>
public class TriggerInfo
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the group.
    /// </summary>
    public string Group { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the cron expression.
    /// </summary>
    public string CronExpression { get; set; }

    /// <summary>
    /// Gets or sets the next fire time.
    /// </summary>
    public DateTimeOffset? NextFireTime { get; set; }

    /// <summary>
    /// Gets the next fire time text.
    /// </summary>
    public string NextFireTimeText => this.NextFireTime.HasValue ? (this.NextFireTime.Value - DateTimeOffset.UtcNow).Humanize() : string.Empty;

    /// <summary>
    /// Gets or sets the previous fire time.
    /// </summary>
    public DateTimeOffset? PreviousFireTime { get; set; }

    /// <summary>
    /// Gets the previous fire time text.
    /// </summary>
    public string PreviousFireTimeText => this.PreviousFireTime.HasValue ? (DateTimeOffset.UtcNow - this.PreviousFireTime.Value).Humanize() : string.Empty;

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public string State { get; set; }
}
