// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using System.Collections.Generic;

[DisallowConcurrentExecution]
[PersistJobDataAfterExecution]
public abstract partial class JobBase : IJob
{
    private const string JobIdKey = "JobId";

    protected JobBase(ILoggerFactory loggerFactory)
    {
        this.Logger = loggerFactory?.CreateLogger(this.GetType()) ??
            NullLoggerFactory.Instance.CreateLogger(this.GetType());
    }

    public ILogger Logger { get; }

    public string Name { get; private set; }

    public Dictionary<string, string> Data { get; private set; }

    /// <summary>
    /// Represents the date and time when the last processing occurred. It includes the time zone offset from UTC.
    /// </summary>
    public DateTimeOffset LastProcessedDate { get; set; }

    /// <summary>
    /// Represents the total elapsed time when the last processing occurred. It can be both retrieved and set.
    /// </summary>
    public long ElapsedMilliseconds { get; set; }

    /// <summary>
    /// Represents the status of a job when the last processing occurred.
    /// </summary>
    public JobStatus Status { get; set; }

    /// <summary>
    /// Holds the error message of when the last processing occurred.
    /// </summary>
    public string ErrorMessage { get; set; }

    public virtual async Task Execute(IJobExecutionContext context)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        var jobId = context.JobDetail.JobDataMap?.GetString(JobIdKey) ?? context.FireInstanceId;
        var jobTypeName = context.JobDetail.JobType.Name;
        var watch = ValueStopwatch.StartNew();
        long elapsedMilliseconds = 0;

        if (context.CancellationToken.IsCancellationRequested)
        {
            this.Logger.LogWarning("{LogKey} processing cancelled (type={JobType}, id={JobId})", Constants.LogKey, jobTypeName, jobId);
            context.CancellationToken.ThrowIfCancellationRequested();
        }
        else
        {
            this.Name = context.JobDetail.Description ?? context.JobDetail.Key.Name;
            BaseTypedLogger.LogProcessing(this.Logger, Constants.LogKey, jobTypeName, this.Name, jobId);

            GetJobProperties(context);

            this.Data = context.JobDetail.JobDataMap.Keys.ToDictionary(k => k, k => context.JobDetail.JobDataMap[k]?.ToString() ?? string.Empty);

            try
            {
                await this.Process(context, context.CancellationToken).AnyContext();
            }
            catch (Exception ex)
            {
                PutJobProperties(context, JobStatus.Fail, $"[{ex.GetType().Name}] {ex.Message}", watch.GetElapsedMilliseconds());

                throw;
            }
            finally
            {
                elapsedMilliseconds = watch.GetElapsedMilliseconds();
            }

            PutJobProperties(context, JobStatus.Success, null, elapsedMilliseconds);
        }

        BaseTypedLogger.LogProcessed(this.Logger, Constants.LogKey, jobTypeName, this.Name, jobId, elapsedMilliseconds);

        void GetJobProperties(IJobExecutionContext context)
        {
            if (context.JobDetail.JobDataMap.TryGetString(nameof(this.Status), out var status))
            {
                Enum.TryParse(status, out JobStatus s);
                this.Status = s;
            }

            if (context.JobDetail.JobDataMap.TryGetString(nameof(this.ErrorMessage), out var errorMessage))
            {
                this.ErrorMessage = errorMessage;
            }

            if (context.JobDetail.JobDataMap.TryGetDateTimeOffset(nameof(this.LastProcessedDate), out var processed))
            {
                this.LastProcessedDate = processed;
            }

            if (context.JobDetail.JobDataMap.TryGetLong(nameof(this.ElapsedMilliseconds), out var elapsed))
            {
                this.ElapsedMilliseconds = elapsed;
            }
        }

        void PutJobProperties(
            IJobExecutionContext context,
            JobStatus status,
            string errorMessage,
            long elapsedMilliseconds)
        {
            this.Status = status;
            this.ErrorMessage = errorMessage;
            this.LastProcessedDate = DateTimeOffset.UtcNow;
            this.ElapsedMilliseconds = elapsedMilliseconds;

            context.JobDetail.JobDataMap.Put(Constants.CorrelationIdKey, context.Get(Constants.CorrelationIdKey));
            context.JobDetail.JobDataMap.Put(Constants.FlowIdKey, context.Get(Constants.FlowIdKey));
            context.JobDetail.JobDataMap.Put(Constants.TriggeredByKey, context.Get(Constants.TriggeredByKey));
            context.JobDetail.JobDataMap.Put(nameof(this.Status), this.Status.ToString());
            context.JobDetail.JobDataMap.Put(nameof(this.ErrorMessage), this.ErrorMessage);
            context.JobDetail.JobDataMap.Put(nameof(this.LastProcessedDate), this.LastProcessedDate);
            context.JobDetail.JobDataMap.Put(nameof(this.ElapsedMilliseconds), this.ElapsedMilliseconds);
        }
    }

    public abstract Task Process(IJobExecutionContext context, CancellationToken cancellationToken = default);

    public static partial class BaseTypedLogger
    {
        [LoggerMessage(0, LogLevel.Information, "{LogKey} processing (type={JobType}, name={JobName}, id={JobId})")]
        public static partial void LogProcessing(ILogger logger, string logKey, string jobType, string jobName, string jobId);

        [LoggerMessage(1, LogLevel.Information, "{LogKey} processed (type={JobType}, name={JobName}, id={JobId}) -> took {TimeElapsed:0.0000} ms")]
        public static partial void LogProcessed(ILogger logger, string logKey, string jobType, string jobName, string jobId, long timeElapsed);
    }
}