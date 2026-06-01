// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Jobs;

using BridgingIT.DevKit.Application.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

public abstract class JobSchedulerTestBase(ITestOutputHelper output)
{
    protected ITestOutputHelper Output { get; } = output;

    protected void ConfigureLogging(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(new XunitLoggerProvider(this.Output));
        });
    }

    protected JobSchedulerTestHarness CreateHarness(
        Action<JobBuilderContext> configureJobs,
        Action<IServiceCollection> configureServices = null,
        Action<JobSchedulerHostedOptions> configureOptions = null,
        DateTimeOffset? nowUtc = null)
    {
        return JobSchedulerTestHarness.Create(
            configureJobs,
            services =>
            {
                this.ConfigureLogging(services);
                configureServices?.Invoke(services);
            },
            configureOptions,
            nowUtc);
    }

    protected JobSchedulerTestHarnessBuilder CreateHarnessBuilder()
    {
        return JobSchedulerTestHarness.Create()
            .WithServices(this.ConfigureLogging);
    }
}
