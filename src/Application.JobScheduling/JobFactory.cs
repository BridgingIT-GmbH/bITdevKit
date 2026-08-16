// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

/// <summary>
/// Represents job factory.
/// </summary>
public class JobFactory : IJobFactory
{
    private readonly IServiceProvider serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <c>JobFactory</c> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used by the operation.</param>
    public JobFactory(IServiceProvider serviceProvider)
    {
        EnsureArg.IsNotNull(serviceProvider, nameof(serviceProvider));

        this.serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Executes the new job operation.
    /// </summary>
    /// <param name="bundle">The bundle used by the operation.</param>
    /// <param name="scheduler">The scheduler used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
    {
        EnsureArg.IsNotNull(bundle, nameof(bundle));

        var job = this.serviceProvider.GetRequiredService(bundle.JobDetail.JobType) as IJob;
        var moduleAccessors = this.serviceProvider.GetServices<IModuleContextAccessor>();

        return new JobWrapper(this.serviceProvider, job, moduleAccessors);
    }

    /// <summary>
    /// Executes the return job operation.
    /// </summary>
    /// <param name="job">The job used by the operation.</param>
    public void ReturnJob(IJob job)
    {
        // the DI container handles this
    }
}
