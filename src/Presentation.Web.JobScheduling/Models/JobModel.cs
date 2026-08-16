// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.JobScheduling;

/// <summary>
/// Represents job model.
/// </summary>
public class JobModel
{
    /// <summary>
    /// Gets or sets the group.
    /// </summary>
    public string Group { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public string Type { get; set; }

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
    /// Gets or sets the trigger type.
    /// </summary>
    public string TriggerType { get; set; }

    /// <summary>
    /// Gets or sets the trigger state.
    /// </summary>
    public string TriggerState { get; set; }

    /// <summary>
    /// Gets or sets the next fire time.
    /// </summary>
    public DateTimeOffset? NextFireTime { get; set; }

    /// <summary>
    /// Gets or sets the previous fire time.
    /// </summary>
    public DateTimeOffset? PreviousFireTime { get; set; }

    /// <summary>
    /// Gets or sets the currently executing.
    /// </summary>
    public bool CurrentlyExecuting { get; set; }

    /// <summary>
    /// Gets or sets the properties.
    /// </summary>
    public Dictionary<string, object> Properties { get; set; }
}
