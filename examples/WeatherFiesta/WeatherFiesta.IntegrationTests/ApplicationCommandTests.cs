// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.IntegrationTests;

/// <summary>
/// Direct command handler tests using IRequester.SendAsync with InMemory EF Core.
/// Tests the full pipeline (validation, retry, timeout, transactions) without HTTP.
/// </summary>
public class ApplicationCommandTests : IClassFixture<WeatherFiestaApplicationFactory>
{
    private readonly WeatherFiestaApplicationFactory factory;
    private readonly IRequester requester;

    public ApplicationCommandTests(WeatherFiestaApplicationFactory factory)
    {
        this.factory = factory;
        this.requester = factory.Services.GetRequiredService<IRequester>();
        factory.SeedAsync().GetAwaiter().GetResult();
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // CityCreateCommand
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CityCreateCommand_WithValidData_ReturnsSuccess()
    {
        // Arrange
        await this.factory.ResetDatabaseAsync();
        this.factory.GeocodingClient
            .SearchCityAsync("Amsterdam", "NL", Arg.Any<CancellationToken>())
            .Returns(new GeocodingResultModel
            {
                Name = "Amsterdam",
                Country = "Netherlands",
                CountryCode = "NL",
                Latitude = 52.3676m,
                Longitude = 4.9041m,
                TimeZone = "Europe/Amsterdam",
                ExternalId = 2759794
            });

        var command = new CityCreateCommand
        {
            Model = new CityCreateModel { Name = "Amsterdam", CountryCode = "NL" }
        };

        // Act
        var result = await this.requester.SendAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Name.ShouldBe("Amsterdam");
    }

    [Fact]
    public async Task CityCreateCommand_WhenGeocodingFails_ReturnsFailure()
    {
        // Arrange
        this.factory.GeocodingClient
            .SearchCityAsync("Unknown", "XX", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<GeocodingResultModel>(null));

        var command = new CityCreateCommand
        {
            Model = new CityCreateModel { Name = "Unknown", CountryCode = "XX" }
        };

        // Act
        var result = await this.requester.SendAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task CityCreateCommand_WhenInvalidData_ReturnsValidationFailure()
    {
        // Arrange — name too short, country code wrong length
        var command = new CityCreateCommand
        {
            Model = new CityCreateModel { Name = "AB", CountryCode = "X" }
        };

        // Act
        var result = await this.requester.SendAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // CityUnsubscribeCommand
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CityUnsubscribeCommand_WhenSubscribed_ReturnsSuccess()
    {
        // Arrange
        await this.factory.ResetDatabaseAsync();
        var command = new CityUnsubscribeCommand(WeatherFiestaTestData.LondonCityGuid.ToString());

        // Act
        var result = await this.requester.SendAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task CityUnsubscribeCommand_WhenNotSubscribed_ReturnsFailure()
    {
        // Arrange — Berlin is not subscribed
        var command = new CityUnsubscribeCommand(WeatherFiestaTestData.BerlinCityGuid.ToString());

        // Act
        var result = await this.requester.SendAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // SetPrimaryCityCommand
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetPrimaryCityCommand_WhenSubscribed_ReturnsSuccess()
    {
        // Arrange
        await this.factory.ResetDatabaseAsync();
        // Add Paris as second city
        this.factory.GeocodingClient
            .SearchCityAsync("Paris", "FR", Arg.Any<CancellationToken>())
            .Returns(new GeocodingResultModel
            {
                Name = "Paris", Country = "France", CountryCode = "FR",
                Latitude = 48.8566m, Longitude = 2.3522m,
                TimeZone = "Europe/Paris", ExternalId = 2988507
            });

        await this.requester.SendAsync(new CityCreateCommand
        {
            Model = new CityCreateModel { Name = "Paris", CountryCode = "FR" }
        });

        // Act — set Paris as primary
        var result = await this.requester.SendAsync(
            new SetPrimaryCityCommand(WeatherFiestaTestData.ParisCityGuid.ToString()));

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task SetPrimaryCityCommand_WhenNotSubscribed_ReturnsFailure()
    {
        // Arrange
        var command = new SetPrimaryCityCommand(WeatherFiestaTestData.BerlinCityGuid.ToString());

        // Act
        var result = await this.requester.SendAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // ReorderCitiesCommand
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReorderCitiesCommand_WithMultipleCities_ReturnsSuccess()
    {
        // Arrange
        await this.factory.ResetDatabaseAsync();
        // Add Paris
        this.factory.GeocodingClient
            .SearchCityAsync("Paris", "FR", Arg.Any<CancellationToken>())
            .Returns(new GeocodingResultModel
            {
                Name = "Paris", Country = "France", CountryCode = "FR",
                Latitude = 48.8566m, Longitude = 2.3522m,
                TimeZone = "Europe/Paris", ExternalId = 2988507
            });

        await this.requester.SendAsync(new CityCreateCommand
        {
            Model = new CityCreateModel { Name = "Paris", CountryCode = "FR" }
        });

        // Act — reorder: Paris first, London second
        var command = new ReorderCitiesCommand
        {
            CityIds =
            [
                WeatherFiestaTestData.ParisCityGuid.ToString(),
                WeatherFiestaTestData.LondonCityGuid.ToString()
            ]
        };
        var result = await this.requester.SendAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // UserProfileUpdateCommand
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UserProfileUpdateCommand_WithValidData_ReturnsSuccess()
    {
        // Arrange
        await this.factory.ResetDatabaseAsync();
        var command = new UserProfileUpdateCommand
        {
            Model = new UserProfileUpdateModel
            {
                Name = "New Name",
                Email = "new@example.com"
            }
        };

        // Act
        var result = await this.requester.SendAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("New Name");
        result.Value.Email.ShouldBe("new@example.com");
    }

    [Fact]
    public async Task UserProfileUpdateCommand_WithInvalidEmail_ReturnsFailure()
    {
        // Arrange
        var command = new UserProfileUpdateCommand
        {
            Model = new UserProfileUpdateModel
            {
                Name = "Valid Name",
                Email = "not-an-email"
            }
        };

        // Act
        var result = await this.requester.SendAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // UserPreferencesUpdateCommand
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UserPreferencesUpdateCommand_WithValidData_ReturnsSuccess()
    {
        // Arrange
        await this.factory.ResetDatabaseAsync();
        var command = new UserPreferencesUpdateCommand
        {
            Model = new UserPreferencesUpdateModel
            {
                TemperatureUnit = "Fahrenheit",
                WindSpeedUnit = "Mph"
            }
        };

        // Act
        var result = await this.requester.SendAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TemperatureUnit.ShouldBe("Fahrenheit");
        result.Value.WindSpeedUnit.ShouldBe("Mph");
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // AdminCityCreateCommand
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AdminCityCreateCommand_WithValidData_ReturnsSuccess()
    {
        // Arrange
        await this.factory.ResetDatabaseAsync();
        var command = new AdminCityCreateCommand
        {
            Model = new AdminCityCreateModel
            {
                Name = "Berlin",
                Country = "Germany",
                CountryCode = "DE",
                TimeZone = "Europe/Berlin",
                Latitude = 52.52m,
                Longitude = 13.405m
            }
        };

        // Act
        var result = await this.requester.SendAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("Berlin");
    }

    [Fact]
    public async Task AdminCityCreateCommand_WithInvalidData_ReturnsFailure()
    {
        // Arrange
        var command = new AdminCityCreateCommand
        {
            Model = new AdminCityCreateModel
            {
                Name = "", // empty
                Country = "Germany",
                CountryCode = "DE",
                TimeZone = "Europe/Berlin"
            }
        };

        // Act
        var result = await this.requester.SendAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // AdminCityDeleteCommand
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AdminCityDeleteCommand_WhenCityExists_ReturnsSuccess()
    {
        // Arrange
        await this.factory.ResetDatabaseAsync();
        var command = new AdminCityDeleteCommand(WeatherFiestaTestData.LondonCityGuid.ToString());

        // Act
        var result = await this.requester.SendAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task AdminCityDeleteCommand_WhenCityNotFound_ReturnsFailure()
    {
        // Arrange
        var command = new AdminCityDeleteCommand(Guid.NewGuid().ToString());

        // Act
        var result = await this.requester.SendAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // AdminUserSubscriptionUpdateCommand
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AdminSubscriptionUpdateCommand_WithValidData_ReturnsSuccess()
    {
        // Arrange
        await this.factory.ResetDatabaseAsync();
        var command = new AdminUserSubscriptionUpdateCommand
        {
            UserId = WeatherFiestaTestData.TestUserId,
            Model = new AdminSubscriptionUpdateModel
            {
                Plan = "Pro",
                Status = "Active",
                BillingCycle = "Monthly"
            }
        };

        // Act
        var result = await this.requester.SendAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Plan.ShouldBe("Pro");
        result.Value.BillingCycle.ShouldBe("Monthly");
    }

    [Fact]
    public async Task AdminSubscriptionUpdateCommand_WithInvalidPlan_ReturnsFailure()
    {
        // Arrange
        var command = new AdminUserSubscriptionUpdateCommand
        {
            UserId = WeatherFiestaTestData.TestUserId,
            Model = new AdminSubscriptionUpdateModel
            {
                Plan = "NonExistent",
                Status = "Active",
                BillingCycle = "Monthly"
            }
        };

        // Act
        var result = await this.requester.SendAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }
}
