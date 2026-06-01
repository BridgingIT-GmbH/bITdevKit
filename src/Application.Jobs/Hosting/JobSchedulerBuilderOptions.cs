// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using Microsoft.Extensions.Hosting;

/// <summary>
/// Provides fluent worker-pool configuration for the scheduler builder.
/// </summary>
public sealed class JobSchedulerWorkerPoolBuilder(JobSchedulerHostedOptions options)
{
    private readonly JobSchedulerHostedOptions options = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// Sets the maximum number of concurrent background workers.
    /// </summary>
    public JobSchedulerWorkerPoolBuilder MaxConcurrency(int value)
    {
        if (value <= 0)
        {
            throw new InvalidOperationException("The scheduler worker pool requires a positive max concurrency.");
        }

        this.options.MaxConcurrency = value;
        return this;
    }

    /// <summary>
    /// Sets the polling interval between scheduler sweeps.
    /// </summary>
    public JobSchedulerWorkerPoolBuilder PollInterval(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            throw new InvalidOperationException("The scheduler worker pool requires a non-negative poll interval.");
        }

        this.options.SweepInterval = value;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of due occurrences accepted from one sweep.
    /// </summary>
    public JobSchedulerWorkerPoolBuilder BatchSize(int value)
    {
        if (value <= 0)
        {
            throw new InvalidOperationException("The scheduler worker pool requires a positive batch size.");
        }

        this.options.BatchSize = value;
        return this;
    }
}

/// <summary>
/// Supplies environment details when composing a scheduler instance identifier.
/// </summary>
public sealed class JobSchedulerInstanceIdContext
{
    /// <summary>
    /// Gets the scheduler environment values.
    /// </summary>
    public required JobSchedulerEnvironmentContext Environment { get; init; }

    internal static JobSchedulerInstanceIdContext Create(IHostEnvironment hostEnvironment)
        => new()
        {
            Environment = JobSchedulerEnvironmentContext.Create(hostEnvironment),
        };
}

/// <summary>
/// Exposes environment values used by scheduler-wide builder callbacks.
/// </summary>
public sealed class JobSchedulerEnvironmentContext
{
    /// <summary>
    /// Gets the current machine name.
    /// </summary>
    public string MachineName { get; init; }

    /// <summary>
    /// Gets the host application name.
    /// </summary>
    public string ApplicationName { get; init; }

    /// <summary>
    /// Gets the host environment name.
    /// </summary>
    public string EnvironmentName { get; init; }

    /// <summary>
    /// Gets the host content root path.
    /// </summary>
    public string ContentRootPath { get; init; }

    internal static JobSchedulerEnvironmentContext Create(IHostEnvironment hostEnvironment)
        => new()
        {
            MachineName = Environment.MachineName,
            ApplicationName = hostEnvironment?.ApplicationName ?? string.Empty,
            EnvironmentName = hostEnvironment?.EnvironmentName ?? string.Empty,
            ContentRootPath = hostEnvironment?.ContentRootPath ?? string.Empty,
        };
}