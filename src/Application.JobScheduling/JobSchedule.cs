// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

public class JobSchedule
{
    public JobSchedule(Type jobType, string cronExpression, string name = null, Dictionary<string, string> data = null)
    {
        EnsureArg.IsNotNull(jobType, nameof(jobType));

        this.JobType = jobType;
        this.CronExpression = cronExpression ?? CronExpressions.Every5Seconds;
        this.Name = name ?? jobType.FullName;
        this.Data = data ?? [];
    }

    public string Name { get; }

    public Type JobType { get; }

    public string CronExpression { get; }

    public Dictionary<string, string> Data { get; }
}