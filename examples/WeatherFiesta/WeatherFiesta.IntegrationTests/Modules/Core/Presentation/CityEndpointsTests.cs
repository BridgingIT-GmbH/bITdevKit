// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.IntegrationTests.Presentation;

/// <summary>
/// Integration tests for city management endpoints.
/// Uses real IRequester pipeline with InMemory EF Core.
/// Only external services (IWeatherGeocodingClient) are mocked.
/// </summary>
[Trait("Category", "Integration")]
[Collection(WeatherFiestaTestCollection.Name)]
public class CityEndpointsTests
{
    private readonly HttpClient client;
    private readonly WeatherFiestaApplicationFactory factory;

    public CityEndpointsTests(WeatherFiestaApplicationFactory factory, ITestOutputHelper output)
    {
        this.factory = factory;
        factory.SetOutput(output);
        factory.ResetDatabaseAsync().GetAwaiter().GetResult();
        this.client = factory.CreateClient();
    }

    [Fact]
    public async Task GetCitySuggestions_ReturnsOk()
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
                        Name = "London",
                        Country = "United Kingdom",
                        CountryCode = "GB",
                        Latitude = 51.5m,
                        Longitude = -0.1m,
                        TimeZone = "Europe/London",
                        ExternalId = 2643743
                    }
                ]
            });

        // Act
        var response = await this.client.GetAsync("/api/core/cities/suggestions?search=London&countryCode=GB");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var suggestions = await response.Content.ReadFromJsonAsync<List<CitySuggestionModel>>();
        suggestions.ShouldNotBeNull();
        suggestions.ShouldNotBeEmpty();
        suggestions[0].Name.ShouldBe("London");
    }

    [Fact]
    public async Task GetCitySuggestions_WhenEmpty_ReturnsOk()
    {
        // Arrange
        this.factory.GeocodingClient
            .SearchCitiesAsync("xyznonexistent", null, Arg.Any<CancellationToken>())
            .Returns(new GeocodingResponseModel { Results = [] });

        // Act
        var response = await this.client.GetAsync("/api/core/cities/suggestions?search=xyznonexistent");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var suggestions = await response.Content.ReadFromJsonAsync<List<CitySuggestionModel>>();
        suggestions.ShouldNotBeNull();
        suggestions.ShouldBeEmpty();
    }

    [Fact]
    public async Task PostCity_WithGeocoding_ReturnsCreated()
    {
        // Arrange
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

        var createModel = new CityCreateModel { Name = "Amsterdam", CountryCode = "NL" };

        // Act
        var response = await this.client.PostAsJsonAsync("/api/core/cities", createModel);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CityModel>();
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Amsterdam");
        result.CountryCode.ShouldBe("NL");
    }

    [Fact]
    public async Task PostCity_WhenGeocodingReturnsNull_ReturnsInternalServerError()
    {
        // Arrange
        this.factory.GeocodingClient
            .SearchCityAsync("NonExistentCity", "XX", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<GeocodingResultModel>(null));

        var createModel = new CityCreateModel { Name = "NonExistentCity", CountryCode = "XX" };

        // Act
        var response = await this.client.PostAsJsonAsync("/api/core/cities", createModel);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetUserCities_ReturnsOk()
    {
        // Arrange — database seeded with London subscription for test user

        // Act
        var response = await this.client.GetAsync("/api/core/cities");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cities = await response.Content.ReadFromJsonAsync<List<UserCityModel>>();
        cities.ShouldNotBeNull();
        cities.ShouldNotBeEmpty();
        cities.ShouldContain(c => c.CityId == TestData.LondonCityGuid.ToString());
        cities.Single(c => c.CityId == TestData.LondonCityGuid.ToString()).LastUpdatedText.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task DeleteCity_Unsubscribes_ReturnsNoContent()
    {
        // Arrange — reset DB so we have a clean state
        await this.factory.ResetDatabaseAsync();
        var cityId = TestData.LondonCityGuid.ToString();

        // Act
        var response = await this.client.DeleteAsync($"/api/core/cities/{cityId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Verify city is no longer in user's subscriptions
        var listResponse = await this.client.GetAsync("/api/core/cities");
        var cities = await listResponse.Content.ReadFromJsonAsync<List<UserCityModel>>();
        cities.ShouldNotContain(c => c.CityId == cityId);
    }

    [Fact]
    public async Task PutCityPrimary_SetsPrimary_ReturnsNoContent()
    {
        // Arrange — add a second city so we can switch primary
        await this.factory.ResetDatabaseAsync();
        this.factory.GeocodingClient
            .SearchCityAsync("Paris", "FR", Arg.Any<CancellationToken>())
            .Returns(new GeocodingResultModel
            {
                Name = "Paris",
                Country = "France",
                CountryCode = "FR",
                Latitude = 48.8566m,
                Longitude = 2.3522m,
                TimeZone = "Europe/Paris",
                ExternalId = 2988507
            });

        await this.client.PostAsJsonAsync("/api/core/cities",
            new CityCreateModel { Name = "Paris", CountryCode = "FR" });

        var parisId = TestData.ParisCityGuid.ToString();

        // Act
        var response = await this.client.PutAsync($"/api/core/cities/{parisId}/primary", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetCityWeather_ReturnsOk()
    {
        // Arrange — seeded user has London subscription
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var range = new DateOnlyRange(today, today.AddDays(1)).ToIsoRangeString();

        // Act
        var response = await this.client.GetAsync(
            $"/api/core/cities/{TestData.LondonCityGuid}/weather?range={range}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var weather = await response.Content.ReadFromJsonAsync<CityWeatherResponse>();
        weather.ShouldNotBeNull();
        weather.ForecastPeriod.ShouldBe(range);
        weather.CurrentWeather.ShouldNotBeNull();
        weather.CurrentWeather.LastUpdatedText.ShouldNotBeNullOrWhiteSpace();
        weather.Forecasts.ShouldNotBeEmpty();
        weather.Forecasts[0].DaylightPeriod.ShouldNotBeNullOrWhiteSpace();
        weather.Forecasts[0].DaylightDurationText.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PostCityIngest_ReturnsNoContent()
    {
        // Arrange — seeded user has London subscription

        // Act
        var response = await this.client.PostAsync(
            $"/api/core/cities/{TestData.LondonCityGuid}/ingest", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
