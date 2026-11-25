namespace BridgingIT.DevKit.Application.UnitTests.Orchestrations;

using System.Globalization;
using BridgingIT.DevKit.Application.Orchestrations;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Verifies orchestration execution settings configured through the builder.</summary>
/// <example><code>services.AddOrchestrations(o => o.StartupDelayInvariant("500ms"));</code></example>
public class OrchestrationBuilderContextTests
{
    /// <summary>Checks opt-in invariant durations and preservation of the legacy startup delay overload.</summary>
    /// <example><code>context.StartupDelayInvariant("1.5m");</code></example>
    [Theory]
    [InlineData("en-US")]
    [InlineData("nl-NL")]
    public void StartupDelayInvariant_WithFriendlyDurations_UsesTheTypedSetter(string culture)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var sut = new OrchestrationBuilderContext(new ServiceCollection()).StartupDelay(1000);

            sut.StartupDelay("500ms").ExecutionSettings.StartupDelay.ShouldBe(TimeSpan.FromSeconds(1));
            sut.StartupDelayInvariant("500ms").ExecutionSettings.StartupDelay.ShouldBe(TimeSpan.FromMilliseconds(500));
            var expected = new OrchestrationBuilderContext(new ServiceCollection()).StartupDelay(TimeSpan.FromSeconds(90));
            sut.StartupDelayInvariant("1.5m").ExecutionSettings.StartupDelay.ShouldBe(expected.ExecutionSettings.StartupDelay);

            foreach (var invalid in new[] { "invalid", null, "1e100h" })
            {
                Should.Throw<ArgumentException>(() => sut.StartupDelayInvariant(invalid)).ParamName.ShouldBe("value");
                sut.ExecutionSettings.StartupDelay.ShouldBe(TimeSpan.FromSeconds(90));
            }

            sut.StartupDelay("00:00:30").ExecutionSettings.StartupDelay.ShouldBe(TimeSpan.FromSeconds(30));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

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
