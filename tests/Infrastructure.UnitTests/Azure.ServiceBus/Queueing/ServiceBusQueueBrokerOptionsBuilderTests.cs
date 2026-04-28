namespace BridgingIT.DevKit.Infrastructure.UnitTests.Azure.ServiceBus.Queueing;

using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Infrastructure.Azure.ServiceBus;

public class ServiceBusQueueBrokerOptionsBuilderTests
{
    [Fact]
    public void Behaviors_WithEnqueuerBehaviors_SetsEnqueuerBehaviors()
    {
        var behavior = Substitute.For<IQueueEnqueuerBehavior>();
        var builder = new ServiceBusQueueBrokerOptionsBuilder();

        var result = builder.Behaviors(new[] { behavior });

        result.ShouldBe(builder);
        builder.Build().EnqueuerBehaviors.ShouldContain(behavior);
    }

    [Fact]
    public void Behaviors_WithHandlerBehaviors_SetsHandlerBehaviors()
    {
        var behavior = Substitute.For<IQueueHandlerBehavior>();
        var builder = new ServiceBusQueueBrokerOptionsBuilder();

        var result = builder.Behaviors(new[] { behavior });

        result.ShouldBe(builder);
        builder.Build().HandlerBehaviors.ShouldContain(behavior);
    }

    [Fact]
    public void HandlerFactory_SetsHandlerFactory()
    {
        var factory = Substitute.For<IQueueMessageHandlerFactory>();
        var builder = new ServiceBusQueueBrokerOptionsBuilder();

        var result = builder.HandlerFactory(factory);

        result.ShouldBe(builder);
        builder.Build().HandlerFactory.ShouldBe(factory);
    }

    [Fact]
    public void Serializer_SetsSerializer()
    {
        var serializer = new SystemTextJsonSerializer();
        var builder = new ServiceBusQueueBrokerOptionsBuilder();

        var result = builder.Serializer(serializer);

        result.ShouldBe(builder);
        builder.Build().Serializer.ShouldBe(serializer);
    }

    [Fact]
    public void ConnectionString_WithValue_SetsConnectionString()
    {
        var builder = new ServiceBusQueueBrokerOptionsBuilder();

        var result = builder.ConnectionString("Endpoint=sb://test.servicebus.windows.net/");

        result.ShouldBe(builder);
        builder.Build().ConnectionString.ShouldBe("Endpoint=sb://test.servicebus.windows.net/");
    }

    [Fact]
    public void ConnectionString_WithNullOrEmpty_DoesNotSetConnectionString()
    {
        var builder = new ServiceBusQueueBrokerOptionsBuilder();

        builder.ConnectionString(null);
        builder.Build().ConnectionString.ShouldBeNull();

        builder.ConnectionString(string.Empty);
        builder.Build().ConnectionString.ShouldBeNull();
    }

    [Fact]
    public void QueueNamePrefix_SetsPrefix()
    {
        var builder = new ServiceBusQueueBrokerOptionsBuilder();

        var result = builder.QueueNamePrefix("bit");

        result.ShouldBe(builder);
        builder.Build().QueueNamePrefix.ShouldBe("bit");
    }

    [Fact]
    public void QueueNameSuffix_SetsSuffix()
    {
        var builder = new ServiceBusQueueBrokerOptionsBuilder();

        var result = builder.QueueNameSuffix("v1");

        result.ShouldBe(builder);
        builder.Build().QueueNameSuffix.ShouldBe("v1");
    }

    [Fact]
    public void MaxConcurrentCalls_SetsValue()
    {
        var builder = new ServiceBusQueueBrokerOptionsBuilder();

        var result = builder.MaxConcurrentCalls(16);

        result.ShouldBe(builder);
        builder.Build().MaxConcurrentCalls.ShouldBe(16);
    }

    [Fact]
    public void PrefetchCount_SetsValue()
    {
        var builder = new ServiceBusQueueBrokerOptionsBuilder();

        var result = builder.PrefetchCount(50);

        result.ShouldBe(builder);
        builder.Build().PrefetchCount.ShouldBe(50);
    }

    [Fact]
    public void AutoCreateQueue_SetsValue()
    {
        var builder = new ServiceBusQueueBrokerOptionsBuilder();

        var result = builder.AutoCreateQueue(false);

        result.ShouldBe(builder);
        builder.Build().AutoCreateQueue.ShouldBeFalse();
    }

    [Fact]
    public void MessageExpiration_SetsValue()
    {
        var expiration = TimeSpan.FromHours(2);
        var builder = new ServiceBusQueueBrokerOptionsBuilder();

        var result = builder.MessageExpiration(expiration);

        result.ShouldBe(builder);
        builder.Build().MessageExpiration.ShouldBe(expiration);
    }

    [Fact]
    public void MaxDeliveryAttempts_SetsValue()
    {
        var builder = new ServiceBusQueueBrokerOptionsBuilder();

        var result = builder.MaxDeliveryAttempts(10);

        result.ShouldBe(builder);
        builder.Build().MaxDeliveryAttempts.ShouldBe(10);
    }

    [Fact]
    public void ProcessDelay_SetsValue()
    {
        var builder = new ServiceBusQueueBrokerOptionsBuilder();

        var result = builder.ProcessDelay(500);

        result.ShouldBe(builder);
        builder.Build().ProcessDelay.ShouldBe(500);
    }

    [Fact]
    public void Build_ReturnsOptionsWithDefaults()
    {
        var options = new ServiceBusQueueBrokerOptionsBuilder().Build();

        options.MaxConcurrentCalls.ShouldBe(8);
        options.PrefetchCount.ShouldBe(20);
        options.AutoCreateQueue.ShouldBeTrue();
        options.MaxDeliveryAttempts.ShouldBe(5);
        options.ProcessDelay.ShouldBe(100);
    }
}
