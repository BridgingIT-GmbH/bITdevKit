// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.IntegrationTests.Presentation;

/// <summary>
/// Integration tests for the dashboard endpoint.
/// Uses real IRequester pipeline with InMemory EF Core.
/// </summary>
[Trait("Category", "Integration")]
[Collection(WeatherFiestaTestCollection.Name)]
public class DashboardEndpointsTests
{
    private readonly HttpClient client;
    private readonly WeatherFiestaApplicationFactory factory;

    public DashboardEndpointsTests(WeatherFiestaApplicationFactory factory, ITestOutputHelper output)
    {
        this.factory = factory;
        factory.SetOutput(output);
        factory.ResetDatabaseAsync().GetAwaiter().GetResult();
        this.client = factory.CreateClient();
    }

    [Fact]
    public async Task GetDashboard_ReturnsOk()
    {
        // Arrange — database seeded with user city subscriptions

        // Act
        var response = await this.client.GetAsync("/api/core/dashboard");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<DashboardModel>();
        content.ShouldNotBeNull();
        content.Cities.ShouldNotBeEmpty();
        content.PrimaryCity.ShouldNotBeNull();
        content.PrimaryCity.CityId.ShouldBe(TestData.LondonCityGuid.ToString());
        content.PrimaryCity.LastUpdatedText.ShouldNotBeNullOrWhiteSpace();
        content.PrimaryCity.CurrentWeather.LastUpdatedText.ShouldNotBeNullOrWhiteSpace();
        content.NextBusinessDayReport.ShouldNotBeNull();
        content.NextBusinessDayReport.ReportType.ShouldBe(WeatherReportType.NextBusinessDay.ToString());
        content.NextBusinessDayReport.Summary.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetDashboard_WhenNoCities_ReturnsOk()
    {
        // Arrange — user with no subscriptions
        await this.factory.ResetDatabaseAsync();
        // Delete the seeded subscription by unsubscribing
        await this.client.DeleteAsync(
            $"/api/core/cities/{TestData.LondonCityGuid}");

        // Act
        var response = await this.client.GetAsync("/api/core/dashboard");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<DashboardModel>();
        content.ShouldNotBeNull();
        content.Cities.ShouldBeEmpty();
        content.NextBusinessDayReport.ShouldBeNull();
    }
}
