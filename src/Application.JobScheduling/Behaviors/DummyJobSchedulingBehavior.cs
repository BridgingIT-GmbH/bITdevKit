// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

public class DummyJobSchedulingBehavior(ILoggerFactory loggerFactory) : JobSchedulingBehaviorBase(loggerFactory)
{
    private const string JobIdKey = "JobId";

    public override async Task Execute(IJobExecutionContext context, JobDelegate next)
    {
        var jobId = context.JobDetail.JobDataMap?.GetString(JobIdKey) ?? context.FireInstanceId;
        var jobTypeName = context.JobDetail.JobType.FullName;

        this.Logger.LogDebug("{LogKey} >>>>> dummy job scheduling behavior - before (type={JobType}, id={JobId})",
            Constants.LogKey,
            jobTypeName,
            jobId);
        await next().AnyContext(); // continue pipeline
        this.Logger.LogDebug("{LogKey} <<<<< dummy job scheduling behavior - after (type={JobType}, id={JobId})",
            Constants.LogKey,
            jobTypeName,
            jobId);
    }
}