namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.Azure.ServiceBus.Queueing;

using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Infrastructure.Azure.ServiceBus;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.DependencyInjection;

[Collection(nameof(TestEnvironmentCollection))]
[IntegrationTest("Infrastructure")]
public class ServiceBusQueueBrokerRegistrationTests
{
    private readonly TestEnvironmentFixture fixture;

    public ServiceBusQueueBrokerRegistrationTests(TestEnvironmentFixture fixture)
    {
        this.fixture = fixture;
    }

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

[Collection(nameof(TestEnvironmentCollection))]
[IntegrationTest("Infrastructure")]
public class ServiceBusQueueBrokerTests
{
    private readonly TestEnvironmentFixture fixture;

    public ServiceBusQueueBrokerTests(TestEnvironmentFixture fixture)
    {
        this.fixture = fixture;
    }

    [SkippableFact]
    public async Task Broker_WhenSubscribed_CreatesQueueAndProcessesMessage()
    {
        Skip.IfNot(await this.fixture.WaitForServiceBusEmulatorReadyAsync(), "Service Bus emulator did not become ready within timeout");

        ServiceBusQueueTestMessageHandler.Reset();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new QueueingRegistrationStore());
        services.AddSingleton(new QueueBrokerControlState());

        var context = new QueueingBuilderContext(services);
        context.WithServiceBusBroker(o => o
            .ConnectionString(this.fixture.ServiceBusEmulatorConnectionString)
            .QueueNamePrefix("test-")
            .AutoCreateQueue(false)
            .MaxConcurrentCalls(1)
            .PrefetchCount(0)
            .MaxDeliveryAttempts(3)
            .ProcessDelay(0));

        var provider = services.BuildServiceProvider();
        await using var broker = provider.GetRequiredService<IQueueBroker>() as IAsyncDisposable;
        var queueBroker = provider.GetRequiredService<IQueueBroker>();

        await queueBroker.Subscribe<ServiceBusQueueTestMessage, ServiceBusQueueTestMessageHandler>();
        await Task.Delay(2000); // give processor time to establish receive link

        var message = new ServiceBusQueueTestMessage("integration-test");
        await queueBroker.Enqueue(message);

        var processed = await WaitForAsync(() => Task.FromResult(ServiceBusQueueTestMessageHandler.LastMessageId == message.MessageId), attempts: 240, delayMilliseconds: 250);

        processed.ShouldBeTrue($"Message {message.MessageId} was not processed within the timeout");
    }

    [SkippableFact]
    public async Task Broker_WhenHandlerNotRegistered_MessageWaitsForHandler()
    {
        Skip.IfNot(await this.fixture.WaitForServiceBusEmulatorReadyAsync(), "Service Bus emulator did not become ready within timeout");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new QueueingRegistrationStore());
        services.AddSingleton(new QueueBrokerControlState());

        var context = new QueueingBuilderContext(services);
        context.WithServiceBusBroker(o => o
            .ConnectionString(this.fixture.ServiceBusEmulatorConnectionString)
            .QueueNamePrefix("test-")
            .AutoCreateQueue(false)
            .MaxConcurrentCalls(1)
            .PrefetchCount(0)
            .ProcessDelay(0));

        var provider = services.BuildServiceProvider();
        await using var broker = provider.GetRequiredService<IQueueBroker>() as IAsyncDisposable;
        var queueBroker = provider.GetRequiredService<IQueueBroker>();
        var brokerService = provider.GetRequiredService<IQueueBrokerService>();

        var message = new ServiceBusQueueTestMessage("waiting-test");
        await queueBroker.Enqueue(message);

        // Give the broker a moment to track the message
        await Task.Delay(500);

        var summary = await brokerService.GetSummaryAsync();
        summary.Total.ShouldBeGreaterThanOrEqualTo(1);
    }

    private static async Task<bool> WaitForAsync(Func<Task<bool>> condition, int attempts = 80, int delayMilliseconds = 250)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (await condition())
            {
                return true;
            }

            await Task.Delay(delayMilliseconds);
        }

        return false;
    }
}

public sealed class ServiceBusQueueTestMessage(string value) : QueueMessageBase
{
    public string Value { get; } = value;
}

public sealed class ServiceBusQueueTestMessageHandler : IQueueMessageHandler<ServiceBusQueueTestMessage>
{
    public static string LastMessageId { get; private set; }

    public static void Reset()
    {
        LastMessageId = null;
    }

    public Task Handle(ServiceBusQueueTestMessage message, CancellationToken cancellationToken)
    {
        LastMessageId = message.MessageId;
        return Task.CompletedTask;
    }
}
