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
    private readonly ConcurrentGroupExecutionListener groupMutualExclusionListener;
    private readonly JobRunHistoryListener jobRunlistener;
    private readonly JobSchedulingOptions options;

    public JobSchedulingService(
        ILoggerFactory loggerFactory,
        ISchedulerFactory schedulerFactory,
        IJobFactory jobFactory,
        IHostApplicationLifetime applicationLifetime,
        IEnumerable<JobSchedule> jobSchedules = null,
        ConcurrentGroupExecutionListener groupMutualExclusionListener = null,
        JobRunHistoryListener jobRunlistener = null,
        JobSchedulingOptions options = null)
    {
        EnsureArg.IsNotNull(schedulerFactory, nameof(schedulerFactory));
        EnsureArg.IsNotNull(jobFactory, nameof(jobFactory));

        this.logger = loggerFactory?.CreateLogger<JobSchedulingService>() ?? NullLoggerFactory.Instance.CreateLogger<JobSchedulingService>();
        this.schedulerFactory = schedulerFactory;
        this.jobFactory = jobFactory;
        this.applicationLifetime = applicationLifetime;
        this.jobSchedules = jobSchedules;
        this.groupMutualExclusionListener = groupMutualExclusionListener;
        this.jobRunlistener = jobRunlistener;
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
                this.logger.LogDebug("{LogKey} scheduling service startup delayed by {Delay}ms", Constants.LogKey, this.options.StartupDelay.TotalMilliseconds);
                await Task.Delay(this.options.StartupDelay, cancellationToken);
            }

            const int maxRetries = 3;
            for (var attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    this.logger.LogInformation("{LogKey} scheduling service starting (attempt={Attempt}/{MaxRetries}, delay={Delay}ms)", Constants.LogKey, attempt, maxRetries, this.options.StartupDelay.TotalMilliseconds);
                    this.Scheduler = await this.schedulerFactory.GetScheduler(cancellationToken).AnyContext();
                    if (this.Scheduler == null)
                    {
                        throw new InvalidOperationException("SchedulerFactory.GetScheduler returned null.");
                    }

                    this.Scheduler.JobFactory = this.jobFactory;
                    if (this.groupMutualExclusionListener != null)
                    {
                        this.Scheduler.ListenerManager.AddJobListener(this.groupMutualExclusionListener);
                    }

                    if (this.jobRunlistener != null)
                    {
                        this.Scheduler.ListenerManager.AddJobListener(this.jobRunlistener);
                    }

                    foreach (var jobSchedule in this.jobSchedules.SafeNull())
                    {
                        if (string.IsNullOrEmpty(jobSchedule.CronExpression))
                        {
                            this.logger.LogWarning("{LogKey} not scheduled, needs a cron expression (name={JobName}, type={JobType})", Constants.LogKey, jobSchedule.Name, jobSchedule.JobType.Name);
                            continue;
                        }

                        var jobDetail = CreateJobDetail(jobSchedule);
                        ITrigger trigger = null;
                        try
                        {
                            trigger = CreateTrigger(jobSchedule);
                        }
                        catch (FormatException)
                        {
                            this.logger.LogWarning("{LogKey} not scheduled, needs a valid cron expression (name={JobName}, type={JobType})", Constants.LogKey, jobSchedule.Name, jobSchedule.JobType.Name);
                            continue;
                        }
                        if (await this.Scheduler.CheckExists(trigger.Key, cancellationToken).AnyContext())
                        {
                            var existingTrigger = await this.Scheduler.GetTrigger(trigger.Key, cancellationToken);
                            if (existingTrigger.Description != trigger.Description) // cron has changed
                            {
                                await this.Scheduler.RescheduleJob(trigger.Key, trigger, cancellationToken).AnyContext();

                                this.logger.LogInformation("{LogKey} rescheduled (name={JobName}, cron={CronExpression}, type={JobType})", Constants.LogKey, jobSchedule.Name, trigger.Description, jobSchedule.JobType.Name);
                            }
                        }

                        if (!await this.Scheduler.CheckExists(jobDetail.Key, cancellationToken).AnyContext())
                        {
                            this.logger.LogInformation("{LogKey} scheduled (name={JobName}, cron={CronExpression}, type={JobType})", Constants.LogKey, jobSchedule.Name, trigger.Description, jobSchedule.JobType.Name);
                            this.logger.LogDebug("{LogKey} scheduled data (name={JobName}): {@JobData}", Constants.LogKey, jobSchedule.Name, jobDetail.JobDataMap.ToDictionary());

                            await this.Scheduler.ScheduleJob(jobDetail, trigger, cancellationToken).AnyContext();
                        }
                        else
                        {
                            this.logger.LogInformation("{LogKey} already scheduled (name={JobName}, cron={CronExpression}, type={JobType})", Constants.LogKey, jobSchedule.Name, trigger.Description, jobSchedule.JobType.Name);
                        }
                    }

                    await this.Scheduler.Start(cancellationToken).AnyContext();

                    this.logger.LogInformation("{LogKey} scheduling service started", Constants.LogKey);
                    break; // Success, exit retry loop
                }
                catch (SchedulerException ex) when (ex.Message.Contains("kill state") || ex.Message.Contains("disconnected"))
                {
                    this.logger.LogWarning(ex, "{LogKey} scheduling service failed due to database issue (attempt {Attempt}/{MaxRetries}). Retrying...", Constants.LogKey, attempt, maxRetries);

                    if (attempt == maxRetries)
                    {
                        this.logger.LogError(ex, "{LogKey} scheduling service failed after {MaxRetries} attempts", Constants.LogKey, maxRetries);
                        throw;
                    }

                    await Task.Delay(1000 * attempt, cancellationToken); // Exponential backoff
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "{LogKey} scheduling service failed unexpectedly: {ErrorMessage}", Constants.LogKey, ex.Message);
                    throw;
                }
            }
        });

        return Task.CompletedTask;
    }

    private static IJobDetail CreateJobDetail(JobSchedule schedule)
    {
        var builder = JobBuilder.Create(schedule.JobType)
            .WithIdentity(schedule.Name, schedule.Group)
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
            .WithIdentity($"{schedule.Name}.trigger", schedule.Group)
            .UsingJobData("TriggerId", GuidGenerator.CreateSequential().ToString("N"))
            .WithCronSchedule(schedule.CronExpression)
            .WithDescription(schedule.CronExpression)
            .Build();
    }
}