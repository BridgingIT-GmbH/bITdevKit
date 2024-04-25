// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using BridgingIT.DevKit.Common;
using EnsureThat;
using Microsoft.Extensions.Logging;
using Quartz;
using System.Threading;
using System.Threading.Tasks;

public abstract partial class JobBase : IJob
{
    private const string JobIdKey = "JobId";

    protected JobBase(ILoggerFactory loggerFactory)
    {
        EnsureArg.IsNotNull(loggerFactory, nameof(loggerFactory));

        this.Logger = loggerFactory.CreateLogger(this.GetType());
    }

    public ILogger Logger { get; }

    public virtual async Task Execute(IJobExecutionContext context)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        var jobId = context.JobDetail.JobDataMap?.GetString(JobIdKey) ?? context.FireInstanceId;
        var jobTypeName = context.JobDetail.JobType.Name;
        var watch = ValueStopwatch.StartNew();

        if (context.CancellationToken.IsCancellationRequested)
        {
            this.Logger.LogWarning("{LogKey} processing cancelled (type={JobType}, id={JobId})", Constants.LogKey, jobTypeName, jobId);
            context.CancellationToken.ThrowIfCancellationRequested();
        }
        else
        {
            TypedLogger.LogProcessing(this.Logger, Constants.LogKey, jobTypeName, jobId);
            await this.Process(context, context.CancellationToken).AnyContext();
        }

        TypedLogger.LogProcessed(this.Logger, Constants.LogKey, jobTypeName, jobId, watch.GetElapsedMilliseconds());
    }

    public abstract Task Process(IJobExecutionContext context, CancellationToken cancellationToken = default);

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Information, "{LogKey} processing (type={JobType}, id={JobId})")]
        public static partial void LogProcessing(ILogger logger, string logKey, string jobType, string jobId);

        [LoggerMessage(1, LogLevel.Information, "{LogKey} processed (type={JobType}, id={JobId}) -> took {TimeElapsed:0.0000} ms")]
        public static partial void LogProcessed(ILogger logger, string logKey, string jobType, string jobId, long timeElapsed);
    }
}