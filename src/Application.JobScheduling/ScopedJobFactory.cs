// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

/// <summary>
/// Represents scoped job factory.
/// </summary>
public class ScopedJobFactory : IJobFactory
{
    private readonly IServiceProvider rootServiceProvider;

    /// <summary>
    /// Initializes a new instance of the <c>ScopedJobFactory</c> class.
    /// </summary>
    /// <param name="rootServiceProvider">The root service provider used by the operation.</param>
    public ScopedJobFactory(IServiceProvider rootServiceProvider)
    {
        EnsureArg.IsNotNull(rootServiceProvider, nameof(rootServiceProvider));

        this.rootServiceProvider = rootServiceProvider;
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

        // Create a new scope for the job, this allows the job to be registered using .AddScoped<T>() which means we can use scoped dependencies (like database contexts)
        var scope = this.rootServiceProvider.CreateScope(); // scope is disposed in JobWrapper:IDisposable?
        var job = (IJob)scope.ServiceProvider.GetRequiredService(bundle.JobDetail.JobType);
        var moduleAccessors = scope.ServiceProvider.GetServices<IModuleContextAccessor>();

        return new ScopedJobWrapper(scope, job, moduleAccessors);
    }

    /// <summary>
    /// Executes the return job operation.
    /// </summary>
    /// <param name="job">The job used by the operation.</param>
    public void ReturnJob(IJob job)
    {
        (job as IDisposable)?.Dispose();
    }
}
