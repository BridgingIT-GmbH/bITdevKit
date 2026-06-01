// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.IntegrationTests;

/// <summary>
/// Direct query handler tests using IRequester.SendAsync with InMemory EF Core.
/// Tests the full pipeline (validation, retry, timeout) without HTTP.
/// </summary>
public class ApplicationQueryTests : IClassFixture<WeatherFiestaApplicationFactory>
{
    private readonly WeatherFiestaApplicationFactory factory;
    private readonly IRequester requester;

    public ApplicationQueryTests(WeatherFiestaApplicationFactory factory)
    {
        this.factory = factory;
        this.requester = factory.Services.GetRequiredService<IRequester>();
        factory.SeedAsync().GetAwaiter().GetResult();
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // UserCitiesQuery
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UserCitiesQuery_WhenCitiesExist_ReturnsSuccess()
    {
        // Arrange — seeded with London subscription

        // Act
        var result = await this.requester.SendAsync(new UserCitiesQuery());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldNotBeEmpty();
        result.Value.ShouldContain(c => c.CityId == WeatherFiestaTestData.LondonCityGuid.ToString());
    }

    [Fact]
    public async Task UserCitiesQuery_WhenNoCities_ReturnsEmpty()
    {
        // Arrange
        await this.factory.ResetDatabaseAsync();
        // Unsubscribe from London
        await this.requester.SendAsync(
            new CityUnsubscribeCommand(WeatherFiestaTestData.LondonCityGuid.ToString()));

        // Act
        var result = await this.requester.SendAsync(new UserCitiesQuery());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // CityWeatherQuery
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CityWeatherQuery_WhenSubscribed_ReturnsSuccess()
    {
        // Arrange — seeded with London subscription

        // Act
        var result = await this.requester.SendAsync(
            new CityWeatherQuery(WeatherFiestaTestData.LondonCityGuid.ToString()));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
    }

    [Fact]
    public async Task CityWeatherQuery_WhenNotSubscribed_ReturnsFailure()
    {
        // Arrange — Berlin not subscribed
        var query = new CityWeatherQuery(WeatherFiestaTestData.BerlinCityGuid.ToString());

        // Act
        var result = await this.requester.SendAsync(query);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // DashboardQuery
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DashboardQuery_WhenCitiesExist_ReturnsSuccess()
    {
        // Arrange

        // Act
        var result = await this.requester.SendAsync(new DashboardQuery());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Cities.ShouldNotBeEmpty();
        result.Value.PrimaryCity.ShouldNotBeNull();
    }

    [Fact]
    public async Task DashboardQuery_WhenNoCities_ReturnsEmpty()
    {
        // Arrange
        await this.factory.ResetDatabaseAsync();
        await this.requester.SendAsync(
            new CityUnsubscribeCommand(WeatherFiestaTestData.LondonCityGuid.ToString()));

        // Act
        var result = await this.requester.SendAsync(new DashboardQuery());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Cities.ShouldBeEmpty();
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // UserProfileQuery
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UserProfileQuery_WhenProfileExists_ReturnsSuccess()
    {
        // Arrange

        // Act
        var result = await this.requester.SendAsync(new UserProfileQuery());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Name.ShouldBe("Test User");
        result.Value.Email.ShouldBe("test@example.com");
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // UserSubscriptionQuery
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UserSubscriptionQuery_WhenSubscriptionExists_ReturnsSuccess()
    {
        // Arrange

        // Act
        var result = await this.requester.SendAsync(new UserSubscriptionQuery());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Plan.ShouldBe("Free");
        result.Value.Status.ShouldBe("Active");
        result.Value.UserId.ShouldBe(WeatherFiestaTestData.TestUserId);
    }

    [Fact]
    public async Task UserSubscriptionQuery_WhenNoSubscription_AutoCreatesFree()
    {
        // Arrange — user without subscription
        await this.factory.ResetDatabaseAsync();
        // Delete the seeded subscription directly from DB
        using var scope = this.factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var subs = db.UserSubscriptions.Where(s => s.UserId == WeatherFiestaTestData.TestUserId).ToList();
        db.UserSubscriptions.RemoveRange(subs);
        await db.SaveChangesAsync();

        // Act
        var result = await this.requester.SendAsync(new UserSubscriptionQuery());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Plan.ShouldBe("Free");
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // AdminCitiesQuery
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AdminCitiesQuery_WhenCitiesExist_ReturnsSuccess()
    {
        // Arrange

        // Act
        var result = await this.requester.SendAsync(new AdminCitiesQuery());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldNotBeEmpty();
        result.Value.ShouldContain(c => c.Name == "London");
        result.Value.ShouldContain(c => c.Name == "Paris");
    }

    [Fact]
    public async Task AdminCitiesQuery_IncludesSubscriptionCount()
    {
        // Arrange

        // Act
        var result = await this.requester.SendAsync(new AdminCitiesQuery());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var london = result.Value.FirstOrDefault(c => c.Name == "London");
        london.ShouldNotBeNull();
        london.SubscriptionCount.ShouldBeGreaterThanOrEqualTo(1);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // AdminUserSubscriptionsQuery
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AdminSubscriptionsQuery_WhenSubscriptionsExist_ReturnsSuccess()
    {
        // Arrange

        // Act
        var result = await this.requester.SendAsync(new AdminUserSubscriptionsQuery());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldNotBeEmpty();
        result.Value.ShouldContain(s => s.UserId == WeatherFiestaTestData.TestUserId);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // AdminUserSubscriptionQuery
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AdminSubscriptionQuery_WhenUserExists_ReturnsSuccess()
    {
        // Arrange
        var query = new AdminUserSubscriptionQuery
        {
            UserId = WeatherFiestaTestData.TestUserId
        };

        // Act
        var result = await this.requester.SendAsync(query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Plan.ShouldBe("Free");
    }

    [Fact]
    public async Task AdminSubscriptionQuery_WhenUserNotFound_ReturnsFailure()
    {
        // Arrange
        var query = new AdminUserSubscriptionQuery
        {
            UserId = Guid.NewGuid().ToString()
        };

        // Act
        var result = await this.requester.SendAsync(query);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // CitySuggestionQuery
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CitySuggestionQuery_WhenResultsFound_ReturnsSuccess()
    {
        // Arrange
        this.factory.GeocodingClient
            .SearchCitiesAsync("London", "GB", Arg.Any<CancellationToken>())
            .Returns(new GeocodingResponseModel
            {
                Results =
                [
                    new()
                    {
                        Name = "London", Country = "United Kingdom", CountryCode = "GB",
                        Latitude = 51.5m, Longitude = -0.1m,
                        TimeZone = "Europe/London", ExternalId = 2643743
                    }
                ]
            });

        var query = new CitySuggestionQuery("London", "GB");

        // Act
        var result = await this.requester.SendAsync(query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeEmpty();
        result.Value[0].Name.ShouldBe("London");
    }

    [Fact]
    public async Task CitySuggestionQuery_WhenNoResults_ReturnsEmpty()
    {
        // Arrange
        this.factory.GeocodingClient
            .SearchCitiesAsync("xyznonexistent", null, Arg.Any<CancellationToken>())
            .Returns(new GeocodingResponseModel { Results = [] });

        var query = new CitySuggestionQuery("xyznonexistent");

        // Act
        var result = await this.requester.SendAsync(query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // CityAlertsQuery
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CityAlertsQuery_ReturnsSuccess()
    {
        // Arrange

        // Act
        var result = await this.requester.SendAsync(new CityAlertsQuery());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // CityRecommendationsQuery
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CityRecommendationsQuery_WhenSubscribed_ReturnsSuccess()
    {
        // Arrange
        var query = new CityRecommendationsQuery(WeatherFiestaTestData.LondonCityGuid.ToString());

        // Act
        var result = await this.requester.SendAsync(query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.CityId.ShouldBe(WeatherFiestaTestData.LondonCityGuid.ToString());
    }

    [Fact]
    public async Task CityRecommendationsQuery_WhenNotSubscribed_ReturnsFailure()
    {
        // Arrange
        var query = new CityRecommendationsQuery(WeatherFiestaTestData.BerlinCityGuid.ToString());

        // Act
        var result = await this.requester.SendAsync(query);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }
}
