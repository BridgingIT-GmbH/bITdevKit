// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.IntegrationTests.Application;

using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;
using Microsoft.Data.SqlClient;

/// <summary>
/// Direct command handler tests using IRequester.SendAsync with SQL Server EF Core.
/// Tests the full pipeline (validation, retry, timeout, transactions) without HTTP.
/// </summary>
[Trait("Category", "Integration")]
[Collection(WeatherFiestaSqlServerCollection.Name)]
public class ApplicationCommandTests : IAsyncLifetime
{
    private readonly WeatherFiestaApplicationTestHost testHost;
    private IRequester requester;

    public ApplicationCommandTests(WeatherFiestaSqlServerFixture fixture, ITestOutputHelper output)
    {
        var connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            InitialCatalog = $"WeatherFiesta_{Guid.NewGuid():N}"
        }.ConnectionString;

        this.testHost = new WeatherFiestaApplicationTestHost(connectionString, output);
    }

    public async Task InitializeAsync()
    {
        this.testHost.Build();
        await this.testHost.ResetDatabaseAsync();
        this.requester = this.testHost.Requester;
    }

    public async Task DisposeAsync()
    {
        await this.testHost.DisposeAsync();
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // CityCreateCommand
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CityCreateCommand_WithValidData_ReturnsSuccess()
    {
        // Arrange
        await this.testHost.ResetDatabaseAsync();
        this.testHost.GeocodingClient
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
        this.testHost.GeocodingClient
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
    public async Task CityCreateCommand_WhenFreePlanCityLimitReached_ReturnsFailure()
    {
        // Arrange - Free plan allows max 3 cities (London seeded + Paris + Berlin = 3)
        await this.testHost.ResetDatabaseAsync();

        await AddUserCityAsync(CityId.Create(TestData.ParisCityGuid), Guid.NewGuid(), 1);
        await AddUserCityAsync(CityId.Create(TestData.BerlinCityGuid), Guid.NewGuid(), 2);

        this.testHost.GeocodingClient
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
        await this.testHost.ResetDatabaseAsync();
        var command = new CityUnsubscribeCommand(TestData.LondonCityGuid.ToString());

        // Act
        var result = await this.requester.SendAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task CityUnsubscribeCommand_WhenNotSubscribed_ReturnsFailure()
    {
        // Arrange — Berlin is not subscribed
        var command = new CityUnsubscribeCommand(TestData.BerlinCityGuid.ToString());

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
        await this.testHost.ResetDatabaseAsync();
        // Add Paris as second city
        this.testHost.GeocodingClient
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
            new SetPrimaryCityCommand(TestData.ParisCityGuid.ToString()));

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task SetPrimaryCityCommand_WhenNotSubscribed_ReturnsFailure()
    {
        // Arrange
        var command = new SetPrimaryCityCommand(TestData.BerlinCityGuid.ToString());

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
        await this.testHost.ResetDatabaseAsync();
        // Add Paris
        this.testHost.GeocodingClient
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
                TestData.ParisCityGuid.ToString(),
                TestData.LondonCityGuid.ToString()
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
        await this.testHost.ResetDatabaseAsync();
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
        await this.testHost.ResetDatabaseAsync();
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
        await this.testHost.ResetDatabaseAsync();
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
        await this.testHost.ResetDatabaseAsync();
        var command = new AdminCityDeleteCommand(TestData.LondonCityGuid.ToString());

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
        await this.testHost.ResetDatabaseAsync();
        var command = new AdminUserSubscriptionUpdateCommand
        {
            UserId = TestData.TestUserId,
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
            UserId = TestData.TestUserId,
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

    // ──────────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────────

    private async Task AddUserCityAsync(CityId cityId, Guid userCityGuid, int displayOrder)
    {
        using var scope = this.testHost.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        if (!dbContext.Cities.Any(c => c.Id == cityId))
        {
            var city = cityId.Value == TestData.ParisCityGuid
                ? TestData.CreateParis()
                : TestData.CreateBerlin();
            city.Id = cityId;
            dbContext.Cities.Add(city);
            await dbContext.SaveChangesAsync();
        }

        var userCity = UserCity.Create(TestData.TestUserId, cityId, isPrimary: false, displayOrder: displayOrder);
        userCity.Id = UserCityId.Create(userCityGuid);
        dbContext.UserCities.Add(userCity);
        await dbContext.SaveChangesAsync();
    }
}
