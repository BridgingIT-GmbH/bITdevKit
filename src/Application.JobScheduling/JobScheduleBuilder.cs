// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class JobScheduleBuilder<TJob>(IServiceCollection services) where TJob : class, IJob
{
    private readonly IServiceCollection services = services ?? throw new ArgumentNullException(nameof(services));
    private readonly Type jobType = typeof(TJob);
    private string cronExpression = CronExpressions.Every5Seconds; // Default
    private string name; // Optional, null by default
    private readonly Dictionary<string, string> data = [];
    private bool enabled = true;

    public JobScheduleBuilder<TJob> Cron(string cronExpression)
    {
        this.cronExpression = string.IsNullOrEmpty(cronExpression)
            ? CronExpressions.Every5Seconds
            : cronExpression;
        return this;
    }

    public JobScheduleBuilder<TJob> Named(string name)
    {
        this.name = name; // Can be null, making it optional
        return this;
    }

    public JobScheduleBuilder<TJob> WithData(string key, string value)
    {
        if (!string.IsNullOrEmpty(key))
        {
            this.data[key] = value; // Overwrites if key exists, adds if new
        }
        return this;
    }

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

    public JobScheduleBuilder<TJob> Enabled(bool enabled = true)
    {
        this.enabled = enabled;
        return this;
    }

    public JobSchedulingBuilderContext RegisterScoped()
    {
        if (this.enabled)
        {
            this.services.AddScoped<TJob>();
            this.services.AddSingleton(new JobSchedule(this.jobType, this.cronExpression, this.name, this.data));
        }

        return new JobSchedulingBuilderContext(this.services);
    }

    public JobSchedulingBuilderContext RegisterSingleton()
    {
        if (this.enabled)
        {
            this.services.AddSingleton<TJob>();
            this.services.AddSingleton(new JobSchedule(this.jobType, this.cronExpression, this.name, this.data));
        }

        return new JobSchedulingBuilderContext(this.services);
    }
}