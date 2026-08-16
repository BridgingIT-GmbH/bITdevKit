// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using System.Collections.Generic;

/// <summary>
/// Provides the base execution pipeline for scheduled jobs.
/// </summary>
[DisallowConcurrentExecution]
[PersistJobDataAfterExecution]
public abstract partial class JobBase : IJob
{
    private const string JobIdKey = "JobId";

    /// <summary>
    /// Initializes a new instance of the <c>JobBase</c> class.
    /// </summary>
    /// <param name="loggerFactory">The factory used to create loggers.</param>
    protected JobBase(ILoggerFactory loggerFactory)
    {
        this.Logger = loggerFactory?.CreateLogger(this.GetType()) ??
            NullLoggerFactory.Instance.CreateLogger(this.GetType());
    }

    /// <summary>
    /// Gets the logger.
    /// </summary>
    public ILogger Logger { get; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Gets or sets the data.
    /// </summary>
    public Dictionary<string, string> Data { get; private set; }

    /// <summary>
    /// Represents the date and time when the last processing occurred.
    /// </summary>
    public DateTimeOffset RunDate { get; set; }

    /// <summary>
    /// Represents the date and time when the last processing was successful.
    /// </summary>
    public DateTimeOffset RunSuccessDate { get; set; }

    /// <summary>
    /// Represents the total elapsed time when the last processing occurred.
    /// </summary>
    public long ElapsedMilliseconds { get; set; }

    /// <summary>
    /// Represents the status when the last processing occurred.
    /// </summary>
    public JobStatus Status { get; set; }

    /// <summary>
    /// Holds the error message of when the last processing occurred.
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Executes the execute operation.
    /// </summary>
    /// <param name="context">The context for the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task Execute(IJobExecutionContext context)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        var jobId = context.JobDetail.JobDataMap?.GetString(JobIdKey) ?? context.FireInstanceId;
        var jobTypeName = context.JobDetail.JobType.Name;
        var watch = ValueStopwatch.StartNew();
        long elapsedMilliseconds = 0;

        if (context.CancellationToken.IsCancellationRequested)
        {
            this.Logger.LogWarning("[{LogKey}] processing cancelled (type={JobType}, id={JobId})", Constants.LogKey, jobTypeName, jobId);
            context.CancellationToken.ThrowIfCancellationRequested();
        }
        else
        {
            this.Name = context.JobDetail.Description ?? context.JobDetail.Key.Name;
            BaseTypedLogger.LogProcessing(this.Logger, Constants.LogKey, jobTypeName, this.Name, jobId);

            GetJobProperties(context);

            this.Data = context.MergedJobDataMap.Keys.ToDictionary(k => k, k => context.MergedJobDataMap[k]?.ToString() ?? string.Empty);

            try
            {
                await this.Process(context, context.CancellationToken).AnyContext();
            }
            catch (OperationCanceledException oeex)
            {
                BaseTypedLogger.LogInterrupted(this.Logger, Constants.LogKey, jobTypeName, this.Name, jobId);

                await PutJobProperties(context, JobStatus.Interrupted, $"[{oeex.GetType().Name}] {oeex.Message}", watch.GetElapsedMilliseconds());

                return;
            }
            catch (Exception ex)
            {
                await PutJobProperties(context, JobStatus.Failed, $"[{ex.GetType().Name}] {ex.Message}", watch.GetElapsedMilliseconds());

                throw;
            }
            finally
            {
                elapsedMilliseconds = watch.GetElapsedMilliseconds();
            }

            await PutJobProperties(context, JobStatus.Success, null, elapsedMilliseconds);
        }

        BaseTypedLogger.LogProcessed(this.Logger, Constants.LogKey, jobTypeName, this.Name, jobId, elapsedMilliseconds);

        void GetJobProperties(IJobExecutionContext context)
        {
            if (context.MergedJobDataMap.ContainsKey("Last" + nameof(this.Status)) && context.MergedJobDataMap.TryGetString("Last" + nameof(this.Status), out var status))
            {
                Enum.TryParse(status, out JobStatus s);
                this.Status = s;
            }

            if (context.MergedJobDataMap.ContainsKey("Last" + nameof(this.ErrorMessage)) && context.MergedJobDataMap.TryGetString("Last" + nameof(this.ErrorMessage), out var errorMessage))
            {
                this.ErrorMessage = errorMessage;
            }

            if (context.MergedJobDataMap.ContainsKey("Last" + nameof(this.RunDate)) && context.MergedJobDataMap.TryGetDateTimeOffset("Last" + nameof(this.RunDate), out var runDate))
            {
                this.RunDate = runDate;
            }

            if (context.MergedJobDataMap.ContainsKey("Last" + nameof(this.RunSuccessDate)) && context.MergedJobDataMap.TryGetDateTimeOffset("Last" + nameof(this.RunSuccessDate), out var runSuccessDate))
            {
                this.RunSuccessDate = runSuccessDate;
            }

            if (context.MergedJobDataMap.ContainsKey("Last" + nameof(this.ElapsedMilliseconds)) && context.MergedJobDataMap.TryGetLong("Last" + nameof(this.ElapsedMilliseconds), out var elapsed))
            {
                this.ElapsedMilliseconds = elapsed;
            }
        }

        async Task PutJobProperties(
            IJobExecutionContext context,
            JobStatus status,
            string errorMessage,
            long elapsedMilliseconds)
        {
            this.Status = status;
            this.ErrorMessage = errorMessage;
            this.RunDate = DateTimeOffset.UtcNow;
            if (status == JobStatus.Success)
            {
                this.RunSuccessDate = DateTimeOffset.UtcNow;
            }

            this.ElapsedMilliseconds = elapsedMilliseconds;

#pragma warning disable CS0618 // Type or member is obsolete
            context.Trigger.JobDataMap.Put(Constants.CorrelationIdKey, context.Get(Constants.CorrelationIdKey));
            context.Trigger.JobDataMap.Put(Constants.FlowIdKey, context.Get(Constants.FlowIdKey));
            context.Trigger.JobDataMap.Put(Constants.TriggeredByKey, context.Get(Constants.TriggeredByKey));
            context.Trigger.JobDataMap.Put(nameof(this.Status), this.Status.ToString());
            context.Trigger.JobDataMap.Put(nameof(this.ErrorMessage), this.ErrorMessage);
            context.Trigger.JobDataMap.Put(nameof(this.RunDate), this.RunDate);
            context.Trigger.JobDataMap.Put(nameof(this.RunSuccessDate), this.RunSuccessDate);
            context.Trigger.JobDataMap.Put(nameof(this.ElapsedMilliseconds), this.ElapsedMilliseconds);
#pragma warning restore CS0618 // Type or member is obsolete

            foreach (var key in this.Data.Keys)
            {
                if (context.Trigger.JobDataMap.ContainsKey(key))
                {
                    context.Trigger.JobDataMap.Remove(key);
                }

#pragma warning disable CS0618 // Type or member is obsolete
                context.Trigger.JobDataMap.Put(key, this.Data[key]);
#pragma warning restore CS0618 // Type or member is obsolete
            }

            // Persist Trigger.JobDataMap changes by updating the trigger
            //var updatedTrigger = TriggerBuilder.Create()
            //    .ForJob(context.JobDetail) // Associate with the same job
            //    .WithIdentity(context.Trigger.Key) // Keep the same trigger key
            //    .UsingJobData(context.Trigger.JobDataMap) // Use the updated JobDataMap
            //    .WithSchedule(context.Trigger.GetScheduleBuilder()) // Preserve the original schedule
            //    .StartAt(context.Trigger.StartTimeUtc) // Preserve start time
            //    .Build();
            //await context.Scheduler.RescheduleJob(context.Trigger.Key, updatedTrigger);
            await Task.Delay(0); // Simulate async operation, replace with actual rescheduling if needed

#pragma warning disable CS0618 // Type or member is obsolete
            context.JobDetail.JobDataMap.Put("Last" + nameof(this.Status), this.Status.ToString());
            context.JobDetail.JobDataMap.Put("Last" + nameof(this.ErrorMessage), this.ErrorMessage);
            context.JobDetail.JobDataMap.Put("Last" + nameof(this.RunDate), this.RunDate);
            context.JobDetail.JobDataMap.Put("Last" + nameof(this.RunSuccessDate), this.RunSuccessDate);
            context.JobDetail.JobDataMap.Put("Last" + nameof(this.ElapsedMilliseconds), this.ElapsedMilliseconds);
#pragma warning restore CS0618 // Type or member is obsolete

            //this.Logger.LogDebug("[{LogKey}] Stored Trigger.JobDataMap: {Keys}", Constants.LogKey, string.Join(", ", context.Trigger.JobDataMap.Keys));
            //this.Logger.LogDebug("[{LogKey}] Stored JobDetail.JobDataMap: {Keys}", Constants.LogKey, string.Join(", ", context.JobDetail.JobDataMap.Keys));
        }
    }

    /// <summary>
    /// Executes the process operation.
    /// </summary>
    /// <param name="context">The context for the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public abstract Task Process(IJobExecutionContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Represents base typed logger.
    /// </summary>
    public static partial class BaseTypedLogger
    {
        /// <summary>
        /// Writes a log entry for the processing operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="jobType">The job type used by the operation.</param>
        /// <param name="jobName">The job name used by the operation.</param>
        /// <param name="jobId">The job id used by the operation.</param>
        [LoggerMessage(0, LogLevel.Information, "[{LogKey}] processing (type={JobType}, name={JobName}, id={JobId})")]
        public static partial void LogProcessing(ILogger logger, string logKey, string jobType, string jobName, string jobId);

        /// <summary>
        /// Writes a log entry for the processed operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="jobType">The job type used by the operation.</param>
        /// <param name="jobName">The job name used by the operation.</param>
        /// <param name="jobId">The job id used by the operation.</param>
        /// <param name="timeElapsed">The time elapsed used by the operation.</param>
        [LoggerMessage(1, LogLevel.Information, "[{LogKey}] processed (type={JobType}, name={JobName}, id={JobId}) -> took {TimeElapsed:0.0000} ms")]
        public static partial void LogProcessed(ILogger logger, string logKey, string jobType, string jobName, string jobId, long timeElapsed);

        /// <summary>
        /// Writes a log entry for the interrupted operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="jobType">The job type used by the operation.</param>
        /// <param name="jobName">The job name used by the operation.</param>
        /// <param name="jobId">The job id used by the operation.</param>
        [LoggerMessage(2, LogLevel.Warning, "[{LogKey}] interrupted (type={JobType}, name={JobName}, id={JobId})")]
        public static partial void LogInterrupted(ILogger logger, string logKey, string jobType, string jobName, string jobId);
    }
}
