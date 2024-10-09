// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

public class JobFactory : IJobFactory
{
    private readonly IServiceProvider serviceProvider;

    public JobFactory(IServiceProvider serviceProvider)
    {
        EnsureArg.IsNotNull(serviceProvider, nameof(serviceProvider));

        this.serviceProvider = serviceProvider;
    }

    public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
    {
        EnsureArg.IsNotNull(bundle, nameof(bundle));

        var job = this.serviceProvider.GetRequiredService(bundle.JobDetail.JobType) as IJob;
        var moduleAccessors = this.serviceProvider.GetServices<IModuleContextAccessor>();

        return new JobWrapper(this.serviceProvider, job, moduleAccessors);
    }

    public void ReturnJob(IJob job)
    {
        // the DI container handles this
    }
}