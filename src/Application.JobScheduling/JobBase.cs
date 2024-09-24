// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Quartz;

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

    public DateTimeOffset ProcessedDate { get; set; }

    public long ElapsedMilliseconds { get; set; }

    public JobStatus Status { get; set; }

    public string ErrorMessage { get; set; }

    public virtual async Task Execute(IJobExecutionContext context)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        var jobId = context.JobDetail.JobDataMap?.GetString(JobIdKey) ?? context.FireInstanceId;
        var jobTypeName = context.JobDetail.JobType.FullName;
        var watch = ValueStopwatch.StartNew();
        long elapsedMilliseconds = 0;

        if (context.CancellationToken.IsCancellationRequested)
        {
            this.Logger.LogWarning("{LogKey} processing cancelled (type={JobType}, id={JobId})",
                Constants.LogKey,
                jobTypeName,
                jobId);
            context.CancellationToken.ThrowIfCancellationRequested();
        }
        else
        {
            TypedLogger.LogProcessing(this.Logger, Constants.LogKey, jobTypeName, jobId);

            GetJobProperties(context);

            try
            {
                await this.Process(context, context.CancellationToken).AnyContext();
            }
            catch (Exception ex)
            {
                PutJobProperties(context,
                    JobStatus.Fail,
                    $"[{ex.GetType().Name}] {ex.Message}",
                    watch.GetElapsedMilliseconds());

                throw;
            }
            finally
            {
                elapsedMilliseconds = watch.GetElapsedMilliseconds();
            }

            PutJobProperties(context, JobStatus.Success, null, elapsedMilliseconds);
        }

        TypedLogger.LogProcessed(this.Logger, Constants.LogKey, jobTypeName, jobId, elapsedMilliseconds);

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

            if (context.JobDetail.JobDataMap.TryGetDateTimeOffset(nameof(this.ProcessedDate), out var processed))
            {
                this.ProcessedDate = processed;
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
            this.ProcessedDate = DateTimeOffset.UtcNow;
            this.ElapsedMilliseconds = elapsedMilliseconds;

            context.JobDetail.JobDataMap.Put(Constants.CorrelationIdKey, context.Get(Constants.CorrelationIdKey));
            context.JobDetail.JobDataMap.Put(Constants.FlowIdKey, context.Get(Constants.FlowIdKey));
            context.JobDetail.JobDataMap.Put(Constants.TriggeredByKey, context.Get(Constants.TriggeredByKey));
            context.JobDetail.JobDataMap.Put(nameof(this.Status), this.Status.ToString());
            context.JobDetail.JobDataMap.Put(nameof(this.ErrorMessage), this.ErrorMessage);
            context.JobDetail.JobDataMap.Put(nameof(this.ProcessedDate), this.ProcessedDate);
            context.JobDetail.JobDataMap.Put(nameof(this.ElapsedMilliseconds), this.ElapsedMilliseconds);
        }
    }

    public abstract Task Process(IJobExecutionContext context, CancellationToken cancellationToken = default);

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Information, "{LogKey} processing (type={JobType}, id={JobId})")]
        public static partial void LogProcessing(ILogger logger, string logKey, string jobType, string jobId);

        [LoggerMessage(1,
            LogLevel.Information,
            "{LogKey} processed (type={JobType}, id={JobId}) -> took {TimeElapsed:0.0000} ms")]
        public static partial void LogProcessed(
            ILogger logger,
            string logKey,
            string jobType,
            string jobId,
            long timeElapsed);
    }
}