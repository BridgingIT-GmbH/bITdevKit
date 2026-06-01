// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.IntegrationTests;

/// <summary>
/// Integration tests for weather-related endpoints.
/// Uses real IRequester pipeline with InMemory EF Core.
/// </summary>
public class CoreWeatherEndpointsTests : IClassFixture<WeatherFiestaApplicationFactory>
{
    private readonly HttpClient client;
    private readonly WeatherFiestaApplicationFactory factory;

    public CoreWeatherEndpointsTests(WeatherFiestaApplicationFactory factory)
    {
        this.factory = factory;
        this.client = factory.CreateClient();
        factory.SeedAsync().GetAwaiter().GetResult();
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

        // Act
        var response = await this.client.GetAsync(
            $"/api/core/cities/{WeatherFiestaTestData.LondonCityGuid}/sun?days=1");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var sunData = await response.Content.ReadFromJsonAsync<CitySunResponse>();
        sunData.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExportCities_ReturnsOk()
    {
        // Arrange — export reads subscribed cities from DB

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
    public async Task ExportWeatherForecast_ReturnsOk()
    {
        // Arrange

        // Act
        var response = await this.client.GetAsync(
            $"/api/core/cities/{WeatherFiestaTestData.LondonCityGuid}/weather/export?days=3");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var export = await response.Content.ReadFromJsonAsync<CityExportResponse>();
        export.ShouldNotBeNull();
        export.CsvContent.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetRecommendations_ReturnsOk()
    {
        // Arrange — recommendations query reads subscribed city from DB

        // Act
        var response = await this.client.GetAsync(
            $"/api/core/cities/{WeatherFiestaTestData.LondonCityGuid}/recommendations");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<CityRecommendationsResponse>();
        result.ShouldNotBeNull();
        result.CityId.ShouldBe(WeatherFiestaTestData.LondonCityGuid.ToString());
    }

    [Fact]
    public async Task GetRecommendations_WhenNotSubscribed_ReturnsError()
    {
        // Arrange — use a city ID the user is not subscribed to
        var unsubscribedCityId = WeatherFiestaTestData.BerlinCityGuid.ToString();

        // Act
        var response = await this.client.GetAsync(
            $"/api/core/cities/{unsubscribedCityId}/recommendations");

        // Assert — handler returns failure → 500 (no subscription found)
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }
}
