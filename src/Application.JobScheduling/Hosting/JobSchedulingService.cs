// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using Microsoft.Extensions.Hosting;
using Quartz;

public class JobSchedulingService : BackgroundService
{
    private readonly ILogger<JobSchedulingService> logger;
    private readonly ISchedulerFactory schedulerFactory;
    private readonly IJobFactory jobFactory;
    private readonly IHostApplicationLifetime applicationLifetime;
    private readonly IEnumerable<JobSchedule> jobSchedules;
    private readonly JobSchedulingOptions options;

    public JobSchedulingService(
        ILoggerFactory loggerFactory,
        ISchedulerFactory schedulerFactory,
        IJobFactory jobFactory,
        IHostApplicationLifetime applicationLifetime,
        IEnumerable<JobSchedule> jobSchedules = null,
        JobSchedulingOptions options = null)
    {
        EnsureArg.IsNotNull(schedulerFactory, nameof(schedulerFactory));
        EnsureArg.IsNotNull(jobFactory, nameof(jobFactory));

        this.logger = loggerFactory?.CreateLogger<JobSchedulingService>() ?? NullLoggerFactory.Instance.CreateLogger<JobSchedulingService>();
        this.schedulerFactory = schedulerFactory;
        this.jobFactory = jobFactory;
        this.applicationLifetime = applicationLifetime;
        this.jobSchedules = jobSchedules;
        this.options = options ?? new JobSchedulingOptions();
    }

    public IScheduler Scheduler { get; set; }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (this.Scheduler?.IsShutdown == false)
        {
            this.logger.LogInformation("{LogKey} scheduling service stopping", Constants.LogKey);
            await (this.Scheduler?.Shutdown(cancellationToken)).AnyContext();
            this.logger.LogInformation("{LogKey} scheduling service stopped", Constants.LogKey);
        }

        await base.StopAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!this.options.Enabled)
        {
            return Task.CompletedTask;
        }

        var registration = this.applicationLifetime.ApplicationStarted.Register(async () =>
        {
            if (this.options.StartupDelay.TotalMilliseconds > 0)
            {
                this.logger.LogDebug("{LogKey} scheduling service startup delayed", Constants.LogKey);
                await Task.Delay(this.options.StartupDelay, cancellationToken);
            }

            try
            {
                this.logger.LogInformation("{LogKey} scheduling service starting", Constants.LogKey);
                this.Scheduler = await this.schedulerFactory.GetScheduler(cancellationToken).AnyContext();
                this.Scheduler.JobFactory = this.jobFactory;

                foreach (var jobSchedule in this.jobSchedules.SafeNull())
                {
                    if (string.IsNullOrEmpty(jobSchedule.CronExpression))
                    {
                        this.logger.LogWarning("{LogKey} not scheduled, needs a cron expression (name={JobName}, type={JobType})", Constants.LogKey, jobSchedule.Name, jobSchedule.JobType.Name);
                        continue;
                    }

                    try
                    {
                        var jobDetail = CreateJobDetail(jobSchedule);
                        var trigger = CreateTrigger(jobSchedule);
                        var jobName = jobSchedule.Name;

                        if (await this.Scheduler.CheckExists(trigger.Key, cancellationToken).AnyContext())
                        {
                            var existingTrigger = await this.Scheduler.GetTrigger(trigger.Key, cancellationToken);
                            if (existingTrigger.Description != trigger.Description) // cron has changed
                            {
                                await this.Scheduler.RescheduleJob(trigger.Key, trigger, cancellationToken).AnyContext();
                                this.logger.LogInformation("{LogKey} rescheduled (name={JobName}, cron={CronExpression}, type={JobType})", Constants.LogKey, jobName, trigger.Description, jobSchedule.JobType.Name);
                            }
                        }

                        if (!await this.Scheduler.CheckExists(jobDetail.Key, cancellationToken).AnyContext())
                        {
                            try
                            {
                                this.logger.LogInformation("{LogKey} scheduled (name={JobName}, cron={CronExpression}, type={JobType})", Constants.LogKey, jobName, trigger.Description, jobSchedule.JobType.Name);
                                this.logger.LogDebug("{LogKey} scheduled data (name={JobName}): {@JobData}", Constants.LogKey, jobName, jobDetail.JobDataMap.ToDictionary());
                                await this.Scheduler.ScheduleJob(jobDetail, trigger, cancellationToken).AnyContext();
                            }
                            catch (ObjectAlreadyExistsException ex)
                            {
                                this.logger.LogError(ex, "{LogKey} schedule failed: {ErrorMessage} (name={JobName}, type={JobType})", Constants.LogKey, ex.Message, jobName, jobSchedule.JobType.Name);
                            }
                        }
                        else
                        {
                            this.logger.LogInformation("{LogKey} scheduled (name={JobName}, cron={CronExpression}, type={JobType})", Constants.LogKey, jobName, trigger.Description, jobSchedule.JobType.Name);
                        }
                    }
                    catch (FormatException ex)
                    {
                        this.logger.LogWarning("{LogKey} not scheduled, invalid cron expression '{CronExpression}' for job (name={JobName}, type={JobType}): {ErrorMessage}", Constants.LogKey, jobSchedule.CronExpression, jobSchedule.Name, jobSchedule.JobType.Name, ex.Message);
                    }
                    catch (SchedulerException ex)
                    {
                        this.logger.LogWarning("{LogKey} schedule failed (name={JobName}, cron={CronExpression}, type={JobType}): {ErrorMessage}", Constants.LogKey, jobSchedule.Name, jobSchedule.CronExpression, jobSchedule.JobType.Name, ex.Message);
                    }
                }

                await this.Scheduler.Start(cancellationToken).AnyContext();
                this.logger.LogInformation("{LogKey} scheduling service started", Constants.LogKey);
            }
            catch (SchedulerException ex)
            {
                this.logger.LogError(ex, "{LogKey} scheduling service failed: {ErrorMessage}", Constants.LogKey, ex.Message);
            }
        });

        return Task.CompletedTask;
    }

    private static IJobDetail CreateJobDetail(JobSchedule schedule)
    {
        var builder = JobBuilder.Create(schedule.JobType)
            .WithIdentity(schedule.Name)
            .UsingJobData("JobId", GuidGenerator.CreateSequential().ToString("N"))
            .WithDescription(schedule.Name ?? schedule.JobType.Name)
            .StoreDurably();

        foreach (var item in schedule.Data.SafeNull())
        {
            builder.UsingJobData(item.Key, item.Value);
        }

        return builder.Build();
    }

    private static ITrigger CreateTrigger(JobSchedule schedule)
    {
        return TriggerBuilder.Create()
            .WithIdentity($"{schedule.Name}.trigger")
            .UsingJobData("TriggerId", GuidGenerator.CreateSequential().ToString("N"))
            .WithCronSchedule(schedule.CronExpression)
            .WithDescription(schedule.CronExpression)
            .Build();
    }
}