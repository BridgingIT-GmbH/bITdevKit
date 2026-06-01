// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.IntegrationTests.Presentation;

/// <summary>
/// Integration tests for admin city management endpoints.
/// Uses real IRequester pipeline with InMemory EF Core.
/// TestAuthenticationHandler grants CoreAdmin role.
/// </summary>
[Trait("Category", "Integration")]
[Collection(WeatherFiestaTestCollection.Name)]
public class AdminEndpointsTests
{
    private readonly HttpClient client;
    private readonly WeatherFiestaApplicationFactory factory;

    public AdminEndpointsTests(WeatherFiestaApplicationFactory factory, ITestOutputHelper output)
    {
        this.factory = factory;
        factory.SetOutput(output);
        factory.ResetDatabaseAsync().GetAwaiter().GetResult();
        this.client = factory.CreateClient();
    }

    [Fact]
    public async Task AdminCreateCity_ReturnsCreated()
    {
        // Arrange
        await this.factory.ResetDatabaseAsync();
        var createModel = new AdminCityCreateModel
        {
            Name = "Berlin",
            Country = "Germany",
            CountryCode = "DE",
            TimeZone = "Europe/Berlin",
            Latitude = 52.52m,
            Longitude = 13.405m
        };

        // Act
        var response = await this.client.PostAsJsonAsync("/api/core/admin/cities", createModel);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CityModel>();
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Berlin");
        result.CountryCode.ShouldBe("DE");
    }

    [Fact]
    public async Task AdminCreateCity_WhenValidationFails_ReturnsBadRequest()
    {
        // Arrange — missing required fields triggers validation error
        var createModel = new AdminCityCreateModel
        {
            Name = "X", // too short (min 2)
            Country = "", // empty
            CountryCode = "D", // not 2 chars
            TimeZone = "Europe/Berlin"
        };

        // Act
        var response = await this.client.PostAsJsonAsync("/api/core/admin/cities", createModel);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AdminListCities_ReturnsOk()
    {
        // Arrange — database already seeded with cities

        // Act
        var response = await this.client.GetAsync("/api/core/admin/cities");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cities = await response.Content.ReadFromJsonAsync<List<AdminCityModel>>();
        cities.ShouldNotBeNull();
        cities.ShouldNotBeEmpty();
        cities.ShouldContain(c => c.Name == "London");
        cities.ShouldContain(c => c.Name == "Paris");
    }

    [Fact]
    public async Task AdminGetCity_ReturnsOk()
    {
        // Arrange
        var cityId = TestData.LondonCityGuid.ToString();

        // Act
        var response = await this.client.GetAsync($"/api/core/admin/cities/{cityId}/subscriptions");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var subscriptions = await response.Content.ReadFromJsonAsync<List<AdminCitySubscriptionModel>>();
        subscriptions.ShouldNotBeNull();
        subscriptions.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task AdminUpdateCity_ReturnsOk()
    {
        // Arrange
        await this.factory.ResetDatabaseAsync();
        var cityId = TestData.LondonCityGuid.ToString();
        var updateModel = new AdminCityUpdateModel
        {
            Name = "London Updated",
            Country = "United Kingdom",
            CountryCode = "GB",
            TimeZone = "Europe/London",
            Latitude = 51.5074m,
            Longitude = -0.1278m
        };

        // Act
        var response = await this.client.PutAsJsonAsync($"/api/core/admin/cities/{cityId}", updateModel);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<CityModel>();
        result.ShouldNotBeNull();
        result.Name.ShouldBe("London Updated");
    }

    [Fact]
    public async Task AdminDeleteCity_ReturnsNoContent()
    {
        // Arrange
        await this.factory.ResetDatabaseAsync();
        var cityId = TestData.LondonCityGuid.ToString();

        // Act
        var response = await this.client.DeleteAsync($"/api/core/admin/cities/{cityId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AdminIngestCity_ReturnsNoContent()
    {
        // Arrange
        var cityId = TestData.LondonCityGuid.ToString();

        // Act
        var response = await this.client.PostAsync(
            $"/api/core/admin/cities/{cityId}/ingest", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AdminResetWeather_ReturnsNoContent()
    {
        // Arrange
        var cityId = TestData.LondonCityGuid.ToString();

        // Act
        var response = await this.client.DeleteAsync(
            $"/api/core/admin/cities/{cityId}/weather");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
