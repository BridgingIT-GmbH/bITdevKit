// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application;

using System;
using System.Collections.Generic;
using BridgingIT.DevKit.Application.JobScheduling;

public class JobScheduleBuilder<TJob>(IServiceCollection services) where TJob : class, IJob
{
    private readonly IServiceCollection services = services ?? throw new ArgumentNullException(nameof(services));
    private readonly Type jobType = typeof(TJob);
    private string cronExpression = CronExpressions.Every5Seconds; // Default
    private string name; // Optional, null by default
    private string group = "DEFAULT";
    private readonly Dictionary<string, string> data = [];
    private bool enabled = true;

    /// <summary>
    /// Sets the cron expression defining the job's execution schedule.
    /// </summary>
    /// <param name="cronExpression">The cron expression (e.g., "0/5 * * * * ?" for every 5 seconds). If null or empty, defaults to every 5 seconds.</param>
    /// <returns>The current JobScheduleBuilder instance for fluent chaining.</returns>
    /// <example>
    /// // Schedule a job to run every minute
    /// services.AddJobScheduling(builder.Configuration)
    ///     .WithJob<EchoJob>()
    ///         .Cron("0 * * * * ?")
    ///         .RegisterScoped();
    /// </example>
    public JobScheduleBuilder<TJob> Cron(string cronExpression)
    {
        this.cronExpression = string.IsNullOrEmpty(cronExpression)
            ? CronExpressions.Every5Seconds
            : cronExpression;
        return this;
    }

    /// <summary>
    /// Assigns a unique name to the job for identification.
    /// </summary>
    /// <param name="name">The name of the job (optional; defaults to the job type name if null).</param>
    /// <returns>The current JobScheduleBuilder instance for fluent chaining.</returns>
    /// <example>
    /// // Name a job explicitly
    /// services.AddJobScheduling(builder.Configuration)
    ///     .WithJob<EchoJob>()
    ///         .Cron("0/10 * * * * ?")
    ///         .Named("echoTask")
    ///         .RegisterScoped();
    /// </example>
    public JobScheduleBuilder<TJob> Named(string name, string group = "DEFAULT")
    {
        this.name = name; // Can be null, making it optional
        this.group = group ?? "DEFAULT";

        return this;
    }

    /// <summary>
    /// Adds a single key-value pair to the job's metadata.
    /// </summary>
    /// <param name="key">The key for the data (must not be null or empty).</param>
    /// <param name="value">The value associated with the key (can be null).</param>
    /// <returns>The current JobScheduleBuilder instance for fluent chaining.</returns>
    /// <example>
    /// // Add metadata to a job
    /// services.AddJobScheduling(builder.Configuration)
    ///     .WithJob<EchoJob>()
    ///         .Cron("0 * * * * ?")
    ///         .WithData("message", "Hello World")
    ///         .RegisterScoped();
    /// </example>
    public JobScheduleBuilder<TJob> WithData(string key, string value)
    {
        if (!string.IsNullOrEmpty(key))
        {
            this.data[key] = value; // Overwrites if key exists, adds if new
        }
        return this;
    }

    /// <summary>
    /// Adds multiple key-value pairs to the job's metadata.
    /// </summary>
    /// <param name="data">A dictionary of key-value pairs to merge into the job's data (null values are ignored).</param>
    /// <returns>The current JobScheduleBuilder instance for fluent chaining.</returns>
    /// <example>
    /// // Add multiple metadata entries
    /// services.AddJobScheduling(builder.Configuration)
    ///     .WithJob<EchoJob>()
    ///         .Cron("0 * * * * ?")
    ///         .WithData(new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } })
    ///         .RegisterScoped();
    /// </example>
    public JobScheduleBuilder<TJob> WithData(Dictionary<string, string> data)
    {
        if (data != null)
        {
            foreach (var kvp in data)
            {
                this.data[kvp.Key] = kvp.Value; // Merges, overwriting duplicates
            }
        }
        return this;
    }

    /// <summary>
    /// Enables or disables the job's execution.
    /// </summary>
    /// <param name="enabled">Whether the job should be enabled (default: true).</param>
    /// <returns>The current JobScheduleBuilder instance for fluent chaining.</returns>
    /// <example>
    /// // Disable a job in production
    /// services.AddJobScheduling(builder.Configuration)
    ///     .WithJob<EchoJob>()
    ///         .Cron("0 * * * * ?")
    ///         .Enabled(builder.Environment.IsDevelopment())
    ///         .RegisterScoped();
    /// </example>
    public JobScheduleBuilder<TJob> Enabled(bool enabled = true)
    {
        this.enabled = enabled;
        return this;
    }

    /// <summary>
    /// Registers the job as a scoped service (default registration method).
    /// </summary>
    /// <returns>A JobSchedulingBuilderContext for further configuration.</returns>
    /// <example>
    /// // Register a scoped job (shorthand)
    /// services.AddJobScheduling(builder.Configuration)
    ///     .WithJob<EchoJob>()
    ///         .Cron("0 * * * * ?")
    ///         .Register();
    /// </example>
    public JobSchedulingBuilderContext Register()
    {
        return this.RegisterScoped();
    }

    /// <summary>
    /// Registers the job as a scoped service, creating a new instance per execution.
    /// </summary>
    /// <returns>A JobSchedulingBuilderContext for further configuration.</returns>
    /// <example>
    /// // Explicitly register a scoped job
    /// services.AddJobScheduling(builder.Configuration)
    ///     .WithJob<EchoJob>()
    ///         .Cron("0/5 * * * * ?")
    ///         .Named("quickEcho")
    ///         .WithData("message", "Fast")
    ///         .RegisterScoped();
    /// </example>
    public JobSchedulingBuilderContext RegisterScoped()
    {
        if (this.enabled)
        {
            this.services.AddScoped<TJob>();
            this.services.AddSingleton(
                new JobSchedule(this.jobType, this.cronExpression, this.name, this.group, this.data));
        }

        return new JobSchedulingBuilderContext(this.services);
    }

    /// <summary>
    /// Registers the job as a singleton service, reusing the same instance across executions.
    /// </summary>
    /// <returns>A JobSchedulingBuilderContext for further configuration.</returns>
    /// <example>
    /// // Register a singleton job for persistent state
    /// services.AddJobScheduling(builder.Configuration)
    ///     .WithJob<MonitorJob>()
    ///         .Cron("0 0 * * * ?") // Hourly
    ///         .Named("hourlyMonitor")
    ///         .RegisterSingleton();
    /// </example>
    public JobSchedulingBuilderContext RegisterSingleton()
    {
        if (this.enabled)
        {
            this.services.AddSingleton<TJob>();
            this.services.AddSingleton(
                new JobSchedule(this.jobType, this.cronExpression, this.name, this.group, this.data));
        }

        return new JobSchedulingBuilderContext(this.services);
    }

    /// <summary>
    /// Sets the cron expression for the job schedule.
    /// </summary>
    /// <param name="cronExpression">The cron expression string to set.</param>
    /// <returns>The current JobScheduleBuilder instance for fluent chaining.</returns>
    /// <remarks>
    /// This method is internal to allow extension methods to configure the private cronExpression field.
    /// </remarks>
    internal JobScheduleBuilder<TJob> SetCronExpression(string cronExpression)
    {
        this.cronExpression = cronExpression;
        return this;
    }
}