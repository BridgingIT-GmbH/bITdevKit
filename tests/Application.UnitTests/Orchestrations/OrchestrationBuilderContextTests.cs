namespace BridgingIT.DevKit.Application.UnitTests.Orchestrations;

using BridgingIT.DevKit.Application.Orchestrations;
using Microsoft.Extensions.DependencyInjection;

public class OrchestrationBuilderContextTests
{
    [Fact]
    public void AddOrchestrations_WithExecutionSettingsBuilder_RegistersConfiguredSettings()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOrchestrations(o => o
            .Enabled(false)
            .StartupDelay("00:00:30")
            .BackgroundSweepInterval("00:00:15")
            .BackgroundSweepBatchSize(20));

        using var serviceProvider = services.BuildServiceProvider();
        var settings = serviceProvider.GetRequiredService<OrchestrationExecutionSettings>();

        // Assert
        settings.EnableBackgroundExecution.ShouldBeFalse();
        settings.StartupDelay.ShouldBe(TimeSpan.FromSeconds(30));
        settings.BackgroundSweepInterval.ShouldBe(TimeSpan.FromSeconds(15));
        settings.BackgroundSweepBatchSize.ShouldBe(20);
    }
}
