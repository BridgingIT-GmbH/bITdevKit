// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

public abstract class JobSchedulingBehaviorBase : IJobSchedulingBehavior
{
    protected JobSchedulingBehaviorBase(ILoggerFactory loggerFactory)
    {
        this.Logger = this.Logger = loggerFactory?.CreateLogger(this.GetType()) ??
            NullLoggerFactory.Instance.CreateLogger(this.GetType());
    }

    protected ILogger Logger { get; }

    public abstract Task Execute(IJobExecutionContext context, JobDelegate next);
}