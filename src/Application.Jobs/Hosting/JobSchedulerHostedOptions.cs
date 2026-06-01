// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using Microsoft.Extensions.Hosting;

/// <summary>
/// Configures hosted background scheduler execution over the active Jobs runtime.
/// </summary>
public class JobSchedulerHostedOptions
{
    /// <summary>
    /// Gets or sets the configured scheduler instance identifier.
    /// </summary>
    public string SchedulerInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the factory used to compose the scheduler instance identifier.
    /// </summary>
    public Func<JobSchedulerInstanceIdContext, string> SchedulerInstanceIdFactory { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the background scheduler is enabled.
    /// </summary>
    public bool EnableBackgroundExecution { get; set; } = true;

    /// <summary>
    /// Gets or sets the delay applied after host startup before the polling loop begins.
    /// </summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Gets or sets the interval between background sweeps.
    /// </summary>
    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the maximum number of due occurrences accepted from one sweep.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum number of concurrent background workers.
    /// </summary>
    public int MaxConcurrency { get; set; } = 4;

    /// <summary>
    /// Gets or sets the maximum number of catch-up occurrences materialized per trigger evaluation.
    /// </summary>
    public int MaxCatchUpOccurrences { get; set; } = 100;

    /// <summary>
    /// Gets or sets the duration of an occurrence lease.
    /// </summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the interval between automatic lease renewals for active executions.
    /// Set to <see cref="TimeSpan.Zero"/> to disable automatic renewal.
    /// </summary>
    public TimeSpan LeaseRenewalInterval { get; set; } = TimeSpan.FromSeconds(10);

    internal string ResolveSchedulerInstanceId(IHostEnvironment hostEnvironment)
    {
        if (!string.IsNullOrWhiteSpace(this.SchedulerInstanceId))
        {
            return this.SchedulerInstanceId.Trim();
        }

        var configured = this.SchedulerInstanceIdFactory?.Invoke(JobSchedulerInstanceIdContext.Create(hostEnvironment));
        return string.IsNullOrWhiteSpace(configured)
            ? $"jobs-{Guid.NewGuid():N}"
            : configured.Trim();
    }
}