// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

/// <summary>
/// Builds job scheduling options configuration.
/// </summary>
public class JobSchedulingOptionsBuilder : OptionsBuilderBase<JobSchedulingOptions, JobSchedulingOptionsBuilder>
{
    /// <summary>
    /// Executes the enabled operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public JobSchedulingOptionsBuilder Enabled(bool value = true)
    {
        this.Target.Enabled = value;

        return this;
    }

    /// <summary>
    /// Executes the disabled operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public JobSchedulingOptionsBuilder Disabled()
    {
        this.Target.Enabled = false;

        return this;
    }

    /// <summary>
    /// Executes the startup delay operation.
    /// </summary>
    /// <param name="timespan">The timespan used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public JobSchedulingOptionsBuilder StartupDelay(TimeSpan timespan)
    {
        this.Target.StartupDelay = timespan;

        return this;
    }

    /// <summary>
    /// Executes the startup delay operation.
    /// </summary>
    /// <param name="milliseconds">The milliseconds used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public JobSchedulingOptionsBuilder StartupDelay(int milliseconds)
    {
        this.Target.StartupDelay = TimeSpan.FromMilliseconds(milliseconds);

        return this;
    }

    /// <summary>
    /// Executes the startup delay operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public JobSchedulingOptionsBuilder StartupDelay(string value)
    {
        this.Target.StartupDelay = TimeSpan.Parse(value);

        return this;
    }

    /// <summary>
    /// Executes the disallow concurrent execution default group operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public JobSchedulingOptionsBuilder DisallowConcurrentExecutionDefaultGroup()
    {
        this.Target.GroupOptions ??= new JobGroupOptions();
        this.Target.GroupOptions.DisallowConcurrentExecutionDefaultGroup = true;

        return this;
    }

    /// <summary>
    /// Executes the disallow concurrent execution groups operation.
    /// </summary>
    /// <param name="groups">The groups used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public JobSchedulingOptionsBuilder DisallowConcurrentExecutionGroups(string[] groups)
    {
        this.Target.GroupOptions ??= new JobGroupOptions();
        this.Target.GroupOptions.DisallowConcurrentExecutionGroups = groups;

        return this;
    }

    /// <summary>
    /// Executes the group operation.
    /// </summary>
    /// <param name="options">The options controlling the operation.</param>
    /// <returns>The result of the operation.</returns>
    public JobSchedulingOptionsBuilder Group(JobGroupOptions options)
    {
        this.Target.GroupOptions = options;

        return this;
    }
}
