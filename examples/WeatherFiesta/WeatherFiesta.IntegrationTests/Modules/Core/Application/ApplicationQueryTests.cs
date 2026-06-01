// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.IntegrationTests.Application;

/// <summary>
/// Direct query handler tests using IRequester.SendAsync with InMemory EF Core.
/// Tests the full pipeline (validation, retry, timeout) without HTTP.
/// </summary>
[Trait("Category", "Integration")]
[Collection(WeatherFiestaTestCollection.Name)]
public class ApplicationQueryTests
{
    private readonly WeatherFiestaApplicationFactory factory;
    private readonly IRequester requester;

    public ApplicationQueryTests(WeatherFiestaApplicationFactory factory, ITestOutputHelper output)
    {
        this.factory = factory;
        factory.SetOutput(output);
        factory.ResetDatabaseAsync().GetAwaiter().GetResult();
        this.requester = factory.Services.GetRequiredService<IRequester>();
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
        result.Value.ShouldContain(c => c.CityId == TestData.LondonCityGuid.ToString());
    }

    [Fact]
    public async Task UserCitiesQuery_WhenNoCities_ReturnsEmpty()
    {
        // Arrange
        await this.factory.ResetDatabaseAsync();
        // Unsubscribe from London
        await this.requester.SendAsync(
            new CityUnsubscribeCommand(TestData.LondonCityGuid.ToString()));

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
            new CityWeatherQuery(TestData.LondonCityGuid.ToString()));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
    }

    [Fact]
    public async Task CityWeatherQuery_WhenNotSubscribed_ReturnsFailure()
    {
        // Arrange — Berlin not subscribed
        var query = new CityWeatherQuery(TestData.BerlinCityGuid.ToString());

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
            new CityUnsubscribeCommand(TestData.LondonCityGuid.ToString()));

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
        result.Value.UserId.ShouldBe(TestData.TestUserId);
    }

    [Fact]
    public async Task UserSubscriptionQuery_WhenNoSubscription_AutoCreatesFree()
    {
        // Arrange — user without subscription
        await this.factory.ResetDatabaseAsync();
        // Delete the seeded subscription directly from DB
        using var scope = this.factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var subs = db.UserSubscriptions.Where(s => s.UserId == TestData.TestUserId).ToList();
        db.UserSubscriptions.RemoveRange(subs);
        await db.SaveChangesAsync();

        // Act
        var result = await this.requester.SendAsync(new UserSubscriptionQuery());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Plan.ShouldBe("Free");
    }

    [Fact]
    public async Task UserSubscriptionQuery_WhenSubscriptionSoftDeleted_ReactivatesWithoutDuplicate()
    {
        // Arrange
        await this.factory.ResetDatabaseAsync();
        await SetSubscriptionPlanAsync(SubscriptionPlan.Basic);
        await SoftDeleteSubscriptionAsync();

        // Act
        var result = await this.requester.SendAsync(new UserSubscriptionQuery());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Plan.ShouldBe("Basic");

        using var scope = this.factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var subscriptions = dbContext.UserSubscriptions.Where(s => s.UserId == TestData.TestUserId).ToList();
        subscriptions.Count.ShouldBe(1);
        subscriptions.Single().AuditState.IsDeleted().ShouldBeFalse();
        subscriptions.Single().Status.ShouldBe(SubscriptionStatus.Active);
        subscriptions.Single().EndDate.ShouldBeNull();
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
        result.Value.ShouldContain(s => s.UserId == TestData.TestUserId);
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
            UserId = TestData.TestUserId
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
    // CityCompareQuery (subscription plan enforcement)
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CityCompareQuery_WhenFreePlan_ReturnsFailure()
    {
        // Arrange - Free plan does not allow comparison
        var query = new CityCompareQuery
        {
            CityIds =
            [
                TestData.LondonCityGuid.ToString(),
                TestData.ParisCityGuid.ToString()
            ]
        };

        // Act
        var result = await this.requester.SendAsync(query);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // CityExportQuery (subscription plan enforcement)
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CityExportQuery_WhenFreePlan_ReturnsFailure()
    {
        // Arrange - Free plan does not allow export

        // Act
        var result = await this.requester.SendAsync(new CityExportQuery());

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task CityExportQuery_WhenPlanAllowsExport_ReturnsSuccess()
    {
        // Arrange
        await this.factory.ResetDatabaseAsync();
        await SetSubscriptionPlanAsync(SubscriptionPlan.Basic);

        // Act
        var result = await this.requester.SendAsync(new CityExportQuery());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.CsvContent.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task CityExportQuery_WhenSubscriptionSoftDeleted_ReactivatesWithoutDuplicate()
    {
        // Arrange
        await this.factory.ResetDatabaseAsync();
        await SetSubscriptionPlanAsync(SubscriptionPlan.Basic);
        await SoftDeleteSubscriptionAsync();

        // Act
        var result = await this.requester.SendAsync(new CityExportQuery());

        // Assert
        result.IsSuccess.ShouldBeTrue();

        using var scope = this.factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var subscriptions = dbContext.UserSubscriptions.Where(s => s.UserId == TestData.TestUserId).ToList();
        subscriptions.Count.ShouldBe(1);
        subscriptions.Single().AuditState.IsDeleted().ShouldBeFalse();
        subscriptions.Single().Status.ShouldBe(SubscriptionStatus.Active);
        subscriptions.Single().EndDate.ShouldBeNull();
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // CityWeatherQuery (forecast days clamping)
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CityWeatherQuery_WhenForecastDaysExceedPlanLimit_ClampsForecasts()
    {
        // Arrange - Free plan max 7 forecast days
        await this.factory.ResetDatabaseAsync();
        await SeedLondonForecastsAsync(16);

        // Act
        var result = await this.requester.SendAsync(
            new CityWeatherQuery(TestData.LondonCityGuid.ToString(), forecastDays: 16));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Forecasts.Count.ShouldBeLessThanOrEqualTo(7);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // CityRecommendationsQuery
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CityRecommendationsQuery_WhenSubscribed_ReturnsSuccess()
    {
        // Arrange
        var query = new CityRecommendationsQuery(TestData.LondonCityGuid.ToString());

        // Act
        var result = await this.requester.SendAsync(query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.CityId.ShouldBe(TestData.LondonCityGuid.ToString());
    }

    [Fact]
    public async Task CityRecommendationsQuery_WhenNotSubscribed_ReturnsFailure()
    {
        // Arrange
        var query = new CityRecommendationsQuery(TestData.BerlinCityGuid.ToString());

        // Act
        var result = await this.requester.SendAsync(query);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────────

    private async Task SetSubscriptionPlanAsync(SubscriptionPlan plan)
    {
        using var scope = this.factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var subscription = dbContext.UserSubscriptions.Single(s => s.UserId == TestData.TestUserId);
        var billingCycle = plan == SubscriptionPlan.Free
            ? SubscriptionBillingCycle.Never
            : SubscriptionBillingCycle.Monthly;

        subscription.ChangePlan(plan, billingCycle);
        await dbContext.SaveChangesAsync();
    }

    private async Task SoftDeleteSubscriptionAsync()
    {
        using var scope = this.factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var subscription = dbContext.UserSubscriptions.Single(s => s.UserId == TestData.TestUserId);
        subscription.AuditState.SetDeleted("test", "soft-delete subscription");
        await dbContext.SaveChangesAsync();
    }

    private async Task SeedLondonForecastsAsync(int count)
    {
        using var scope = this.factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var londonId = CityId.Create(TestData.LondonCityGuid);

        // Remove existing London forecasts to avoid duplicates
        var existing = dbContext.WeatherForecasts.Where(f => f.CityId == londonId).ToList();
        dbContext.WeatherForecasts.RemoveRange(existing);

        // Seed forecasts for today through count-1 days
        for (var i = 0; i < count; i++)
        {
            var forecast = WeatherForecast.Create(londonId);
            forecast.ForecastDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(i));
            forecast.DayWeatherCode = 0;
            forecast.TemperatureMax = 20m + i;
            forecast.TemperatureMin = 12m + i;
            forecast.ApparentTemperatureMax = 18m + i;
            forecast.ApparentTemperatureMin = 10m + i;
            forecast.PrecipitationSum = 0m;
            forecast.PrecipitationProbabilityMax = 10;
            forecast.WindSpeedMax = 15m;
            forecast.WindGustsMax = 25m;
            forecast.DominantWindDirection = 180;
            forecast.UvIndexMax = 5m;
            forecast.SunshineDurationSeconds = 28800;
            forecast.DaylightDurationSeconds = 57600;
            forecast.Sunrise = DateTime.UtcNow.Date.AddHours(5);
            forecast.Sunset = DateTime.UtcNow.Date.AddHours(21);
            forecast.RetrievedAt = DateTime.UtcNow;
            dbContext.WeatherForecasts.Add(forecast);
        }

        await dbContext.SaveChangesAsync();
    }
}
