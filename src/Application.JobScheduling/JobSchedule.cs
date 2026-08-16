// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

/// <summary>
/// Represents job schedule.
/// </summary>
public class JobSchedule
{
    /// <summary>
    /// Initializes a new instance of the <c>JobSchedule</c> class.
    /// </summary>
    /// <param name="jobType">The job type used by the operation.</param>
    /// <param name="cronExpression">The cron expression used by the operation.</param>
    /// <param name="name">The name of the value.</param>
    /// <param name="group">The group used by the operation.</param>
    /// <param name="data">The data used by the operation.</param>
    public JobSchedule(Type jobType, string cronExpression, string name = null, string group = null, Dictionary<string, string> data = null)
    {
        EnsureArg.IsNotNull(jobType, nameof(jobType));

        this.JobType = jobType;
        this.CronExpression = cronExpression ?? CronExpressions.Every5Seconds;
        this.Name = name ?? jobType.FullName;
        this.Group = group ?? "DEFAULT";
        this.Data = data ?? [];
    }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the group.
    /// </summary>
    public string Group { get; }

    /// <summary>
    /// Gets the job type.
    /// </summary>
    public Type JobType { get; }

    /// <summary>
    /// Gets the cron expression.
    /// </summary>
    public string CronExpression { get; }

    /// <summary>
    /// Gets the data.
    /// </summary>
    public Dictionary<string, string> Data { get; }
}
