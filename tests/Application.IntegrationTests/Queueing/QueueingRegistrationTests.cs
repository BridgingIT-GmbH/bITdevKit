namespace BridgingIT.DevKit.Application.IntegrationTests.Queueing;

using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Queueing;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

[IntegrationTest("Application")]
public class QueueingRegistrationTests
{
    [Fact]
    public void AddQueueing_WhenCalledMultipleTimes_AccumulatesSubscriptionsAndRegistersSingleHostedService()
    {
        var services = QueueingBrokerTestSupport.CreateServices();

        services.AddQueueing().WithSubscription<FirstQueueMessage, FirstQueueMessageHandler>();
        services.AddQueueing().WithSubscription<SecondQueueMessage, SecondQueueMessageHandler>();

        using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<QueueingRegistrationStore>();
        var hostedServices = provider.GetServices<IHostedService>().OfType<QueueingService>().ToList();

        store.Subscriptions.Count.ShouldBe(2);
        hostedServices.Count.ShouldBe(1);
    }

    [Fact]
    public void AddQueueing_WithEntityFrameworkBroker_RegistersSingleQueueingHostedService()
    {
        var services = QueueingBrokerTestSupport.CreateServices();
        var databaseRoot = new InMemoryDatabaseRoot();
        services.AddTestQueueDbContext($"queueing-runtime-{Guid.NewGuid():N}", databaseRoot);

        services.AddQueueing().WithEntityFrameworkBroker<TestQueueDbContext>(new EntityFrameworkQueueBrokerConfiguration());

        using var provider = services.BuildServiceProvider();
        var hostedServices = provider.GetServices<IHostedService>().ToList();

        hostedServices.OfType<QueueingService>().Count().ShouldBe(1);
        hostedServices.Count.ShouldBe(1);
        provider.GetService<IQueueBrokerBackgroundProcessor>().ShouldNotBeNull();
    }
}