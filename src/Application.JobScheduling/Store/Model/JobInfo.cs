// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

/// <summary>
/// Represents job info.
/// </summary>
public class JobInfo
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
    /// Gets or sets the type.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the trigger count.
    /// </summary>
    public int TriggerCount { get; set; }

    /// <summary>
    /// Gets or sets the last run.
    /// </summary>
    public JobRun LastRun { get; set; }

    /// <summary>
    /// Gets the is running.
    /// </summary>
    public bool IsRunning => this.LastRun?.IsRunning == true;

    /// <summary>
    /// Gets or sets the last run stats.
    /// </summary>
    public JobRunStats LastRunStats { get; set; }

    /// <summary>
    /// Gets or sets the category.
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Gets or sets the triggers.
    /// </summary>
    public IEnumerable<TriggerInfo> Triggers { get; set; }

    /// <summary>
    /// Gets or sets the runs.
    /// </summary>
    public List<JobRun> Runs { get; set; } = [];
}
