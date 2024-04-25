// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using System;
using BridgingIT.DevKit.Common;
using EnsureThat;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Spi;

public class ScopedJobFactory : IJobFactory
{
    private readonly IServiceProvider rootServiceProvider;

    public ScopedJobFactory(IServiceProvider rootServiceProvider)
    {
        EnsureArg.IsNotNull(rootServiceProvider, nameof(rootServiceProvider));

        this.rootServiceProvider = rootServiceProvider;
    }

    public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
    {
        EnsureArg.IsNotNull(bundle, nameof(bundle));

        // Create a new scope for the job, this allows the job to be registered using .AddScoped<T>() which means we can use scoped dependencies (like database contexts)
        var scope = this.rootServiceProvider.CreateScope(); // scope is disposed in JobWrapper:IDisposable?
        var job = (IJob)scope.ServiceProvider.GetRequiredService(bundle.JobDetail.JobType);
        var moduleAccessors = scope.ServiceProvider.GetServices<IModuleContextAccessor>();

        return new ScopedJobWrapper(scope, job, moduleAccessors);
    }

    public void ReturnJob(IJob job)
    {
        (job as IDisposable)?.Dispose();
    }
}