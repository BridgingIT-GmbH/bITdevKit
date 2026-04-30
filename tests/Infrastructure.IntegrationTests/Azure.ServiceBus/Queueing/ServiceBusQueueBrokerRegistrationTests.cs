// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.Azure;

using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Infrastructure.Azure;
using Microsoft.Extensions.DependencyInjection;

[Collection(nameof(TestEnvironmentCollection))]
[IntegrationTest("Infrastructure")]
public class ServiceBusQueueBrokerRegistrationTests(TestEnvironmentFixture fixture)
{
    private readonly TestEnvironmentFixture fixture = fixture;

    [Fact]
    public void WithServiceBusBroker_RegistersBrokerAndService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new QueueingRegistrationStore());
        services.AddSingleton(new QueueBrokerControlState());

        var context = new QueueingBuilderContext(services);
        context.WithServiceBusBroker(o => o.ConnectionString("Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test"));

        var provider = services.BuildServiceProvider();

        provider.GetService<IQueueBroker>().ShouldNotBeNull();
        provider.GetService<IQueueBrokerService>().ShouldNotBeNull();
        provider.GetService<ServiceBusQueueBroker>().ShouldNotBeNull();
        provider.GetService<ServiceBusQueueBrokerService>().ShouldNotBeNull();
    }

    [Fact]
    public void WithServiceBusBroker_UsingConfiguration_BindsValues()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new QueueingRegistrationStore());
        services.AddSingleton(new QueueBrokerControlState());

        var configuration = new ServiceBusQueueBrokerConfiguration
        {
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test",
            QueueNamePrefix = "bit",
            QueueNameSuffix = "v1",
            MaxConcurrentCalls = 16,
            PrefetchCount = 50,
            AutoCreateQueue = false,
            MaxDeliveryAttempts = 10,
            ProcessDelay = 250
        };

        var context = new QueueingBuilderContext(services);
        context.WithServiceBusBroker(configuration);

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<ServiceBusQueueBrokerOptions>();

        options.ConnectionString.ShouldBe(configuration.ConnectionString);
        options.QueueNamePrefix.ShouldBe("bit");
        options.QueueNameSuffix.ShouldBe("v1");
        options.MaxConcurrentCalls.ShouldBe(16);
        options.PrefetchCount.ShouldBe(50);
        options.AutoCreateQueue.ShouldBeFalse();
        options.MaxDeliveryAttempts.ShouldBe(10);
        options.ProcessDelay.ShouldBe(250);
    }

    [Fact]
    public void WithServiceBusBroker_MissingConnectionString_ThrowsOnBuild()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new QueueingRegistrationStore());
        services.AddSingleton(new QueueBrokerControlState());

        var context = new QueueingBuilderContext(services);
        context.WithServiceBusBroker(o => o.ConnectionString(null));

        var provider = services.BuildServiceProvider();

        Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<ServiceBusQueueBroker>());
    }
}