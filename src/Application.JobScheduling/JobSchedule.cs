// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

public class JobSchedule
{
    public JobSchedule(Type jobType, string cronExpression)
    {
        EnsureArg.IsNotNull(jobType, nameof(jobType));

        this.JobType = jobType;
        this.CronExpression = cronExpression ?? CronExpressions.Every5Seconds;
    }

    public Type JobType { get; }

    public string CronExpression { get; }
}