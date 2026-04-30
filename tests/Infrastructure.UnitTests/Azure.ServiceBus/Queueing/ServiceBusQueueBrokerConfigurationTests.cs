namespace BridgingIT.DevKit.Infrastructure.UnitTests.Azure.ServiceBus.Queueing;

using BridgingIT.DevKit.Infrastructure.Azure;
using FluentValidation.TestHelper;

public class ServiceBusQueueBrokerConfigurationTests
{
    [Fact]
    public void Validator_ConnectionStringIsNull_ShouldHaveValidationError()
    {
        var validator = new ServiceBusQueueBrokerConfiguration.Validator();
        var config = new ServiceBusQueueBrokerConfiguration { ConnectionString = null };

        var result = validator.TestValidate(config);

        result.ShouldHaveValidationErrorFor(c => c.ConnectionString);
    }

    [Fact]
    public void Validator_ConnectionStringIsEmpty_ShouldHaveValidationError()
    {
        var validator = new ServiceBusQueueBrokerConfiguration.Validator();
        var config = new ServiceBusQueueBrokerConfiguration { ConnectionString = string.Empty };

        var result = validator.TestValidate(config);

        result.ShouldHaveValidationErrorFor(c => c.ConnectionString);
    }

    [Fact]
    public void Validator_ConnectionStringIsValid_ShouldNotHaveValidationError()
    {
        var validator = new ServiceBusQueueBrokerConfiguration.Validator();
        var config = new ServiceBusQueueBrokerConfiguration
        {
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test"
        };

        var result = validator.TestValidate(config);

        result.ShouldNotHaveValidationErrorFor(c => c.ConnectionString);
    }

    [Fact]
    public void DefaultValues_AreSetCorrectly()
    {
        var config = new ServiceBusQueueBrokerConfiguration();

        config.MaxConcurrentCalls.ShouldBe(8);
        config.PrefetchCount.ShouldBe(20);
        config.AutoCreateQueue.ShouldBeTrue();
        config.MaxDeliveryAttempts.ShouldBe(5);
        config.ProcessDelay.ShouldBe(100);
        config.MessageExpiration.ShouldBe(TimeSpan.FromDays(7));
    }
}
