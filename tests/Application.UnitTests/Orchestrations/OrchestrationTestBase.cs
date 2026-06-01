// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Orchestrations;

using BridgingIT.DevKit.Application.Orchestrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

public abstract class OrchestrationTestBase(ITestOutputHelper output)
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

    protected OrchestrationTestHarnessBuilder CreateHarnessBuilder()
    {
        return OrchestrationTestHarness.CreateBuilder()
            .ConfigureServices(this.ConfigureLogging);
    }
}
