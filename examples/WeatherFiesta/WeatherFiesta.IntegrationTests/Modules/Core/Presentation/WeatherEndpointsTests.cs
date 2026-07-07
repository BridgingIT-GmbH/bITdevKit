// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.IntegrationTests.Presentation;

/// <summary>
/// Integration tests for weather-related endpoints.
/// Uses real IRequester pipeline with InMemory EF Core.
/// </summary>
[Trait("Category", "Integration")]
[Collection(WeatherFiestaTestCollection.Name)]
public class WeatherEndpointsTests
{
    private readonly HttpClient client;
    private readonly WeatherFiestaApplicationFactory factory;

    public WeatherEndpointsTests(WeatherFiestaApplicationFactory factory, ITestOutputHelper output)
    {
        this.factory = factory;
        factory.SetOutput(output);
        factory.ResetDatabaseAsync().GetAwaiter().GetResult();
        this.client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAlerts_ReturnsOk()
    {
        // Arrange — alerts query reads from DB; seeded user has a city subscription

        // Act
        var response = await this.client.GetAsync("/api/core/cities/alerts");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var alerts = await response.Content.ReadFromJsonAsync<List<CityAlertsModel>>();
        alerts.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetSunData_ReturnsOk()
    {
        // Arrange — seeded user has London subscription
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var range = new DateOnlyRange(today, today.AddDays(1)).ToIsoRangeString();

        // Act
        var response = await this.client.GetAsync(
            $"/api/core/cities/{TestData.LondonCityGuid}/sun?range={range}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var sunData = await response.Content.ReadFromJsonAsync<CitySunResponse>();
        sunData.ShouldNotBeNull();
        sunData.Period.ShouldBe(range);
        sunData.SunData.ShouldNotBeEmpty();
        sunData.SunData[0].DaylightPeriod.ShouldNotBeNullOrWhiteSpace();
        sunData.SunData[0].DaylightDurationText.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CompareCities_WhenFreePlan_ReturnsBadRequest()
    {
        // Act
        var response = await this.client.PostAsJsonAsync(
            "/api/core/cities/compare",
            new[]
            {
                TestData.LondonCityGuid.ToString(),
                TestData.ParisCityGuid.ToString()
            });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExportCities_WhenFreePlan_ReturnsBadRequest()
    {
        // Act
        var response = await this.client.GetAsync("/api/core/cities/export");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExportCities_WhenPlanAllowsExport_ReturnsOk()
    {
        // Arrange
        await this.SetSubscriptionPlanAsync(SubscriptionPlan.Basic);

        // Act
        var response = await this.client.GetAsync("/api/core/cities/export");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var export = await response.Content.ReadFromJsonAsync<CityExportResponse>();
        export.ShouldNotBeNull();
        export.CsvContent.ShouldNotBeNullOrEmpty();
        export.FileName.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExportWeatherForecast_WhenFreePlan_ReturnsOk()
    {
        // Act
        var response = await this.client.GetAsync(
            $"/api/core/cities/{TestData.LondonCityGuid}/weather/export?days=3");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var csv = await response.Content.ReadAsStringAsync();
        csv.ShouldNotBeNullOrWhiteSpace();
        csv.ShouldContain("ForecastDate");
    }

    [Fact]
    public async Task ExportWeatherForecast_WhenPlanAllowsExport_ReturnsOk()
    {
        // Arrange
        await this.SetSubscriptionPlanAsync(SubscriptionPlan.Basic);

        // Act
        var response = await this.client.GetAsync(
            $"/api/core/cities/{TestData.LondonCityGuid}/weather/export?days=3");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType.ShouldNotBeNull();
        response.Content.Headers.ContentType.MediaType.ShouldBe("text/csv");
        response.Content.Headers.ContentDisposition.ShouldNotBeNull();
        response.Content.Headers.ContentDisposition.FileName.ShouldContain("weather-forecasts");

        var csv = await response.Content.ReadAsStringAsync();
        csv.ShouldNotBeNullOrWhiteSpace();
        csv.ShouldContain("ForecastDate");
    }

    [Fact]
    public async Task GetRecommendations_ReturnsOk()
    {
        // Arrange — recommendations query reads subscribed city from DB

        // Act
        var response = await this.client.GetAsync(
            $"/api/core/cities/{TestData.LondonCityGuid}/recommendations");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<CityRecommendationsResponse>();
        result.ShouldNotBeNull();
        result.CityId.ShouldBe(TestData.LondonCityGuid.ToString());
        result.LastUpdatedText.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetRecommendations_WhenNotSubscribed_ReturnsError()
    {
        // Arrange — use a city ID the user is not subscribed to
        var unsubscribedCityId = TestData.BerlinCityGuid.ToString();

        // Act
        var response = await this.client.GetAsync(
            $"/api/core/cities/{unsubscribedCityId}/recommendations");

        // Assert — handler returns failure → 500 (no subscription found)
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

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
}
