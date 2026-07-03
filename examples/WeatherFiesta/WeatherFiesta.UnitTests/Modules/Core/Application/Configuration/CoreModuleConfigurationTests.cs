// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.UnitTests.Modules.Core.Configuration;

using BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Unit tests for <see cref="CoreModuleConfiguration"/>.
/// </summary>
public class CoreModuleConfigurationTests
{
    [Fact]
    public void Validator_ValidJobsCron_ReturnsSuccess()
    {
        // Arrange
        var sut = new CoreModuleConfiguration.Validator(new CronosJobCronEngine());
        var configuration = CreateConfiguration("*/30 * * * *");

        // Act
        var result = sut.Validate(configuration);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-cron")]
    [InlineData("1 2 3 4 5 6 7")]
    public void Validator_InvalidJobsCron_ReturnsFailure(string cron)
    {
        // Arrange
        var sut = new CoreModuleConfiguration.Validator(new CronosJobCronEngine());
        var configuration = CreateConfiguration(cron);

        // Act
        var result = sut.Validate(configuration);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == $"{nameof(CoreModuleConfiguration.Jobs)}.{nameof(WeatherJobOptions.IngestionCron)}");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validator_InvalidCleanupRetentionDays_ReturnsFailure(int retentionDays)
    {
        // Arrange
        var sut = new CoreModuleConfiguration.Validator(new CronosJobCronEngine());
        var configuration = CreateConfiguration("*/30 * * * *");
        configuration.Jobs.CleanupRetentionDays = retentionDays;

        // Act
        var result = sut.Validate(configuration);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == $"{nameof(CoreModuleConfiguration.Jobs)}.{nameof(WeatherJobOptions.CleanupRetentionDays)}");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-cron")]
    [InlineData("1 2 3 4 5 6 7")]
    public void Validator_InvalidCleanupCron_ReturnsFailure(string cron)
    {
        // Arrange
        var sut = new CoreModuleConfiguration.Validator(new CronosJobCronEngine());
        var configuration = CreateConfiguration("*/30 * * * *");
        configuration.Jobs.CleanupCron = cron;

        // Act
        var result = sut.Validate(configuration);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == $"{nameof(CoreModuleConfiguration.Jobs)}.{nameof(WeatherJobOptions.CleanupCron)}");
    }

    private static CoreModuleConfiguration CreateConfiguration(string cron)
    {
        return new CoreModuleConfiguration
        {
            ConnectionStrings = new Dictionary<string, string> { ["Default"] = "Server=(localdb)\\mssqllocaldb;Database=WeatherFiesta;Trusted_Connection=True;" },
            Jobs = new WeatherJobOptions
            {
                IngestionCron = cron
            }
        };
    }
}
