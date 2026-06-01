// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.IntegrationTests.Presentation;

/// <summary>
/// Integration tests for user profile and preferences endpoints.
/// Uses real IRequester pipeline with InMemory EF Core.
/// </summary>
[Trait("Category", "Integration")]
[Collection(WeatherFiestaTestCollection.Name)]
public class UserEndpointsTests
{
    private readonly HttpClient client;
    private readonly WeatherFiestaApplicationFactory factory;

    public UserEndpointsTests(WeatherFiestaApplicationFactory factory, ITestOutputHelper output)
    {
        this.factory = factory;
        factory.SetOutput(output);
        factory.ResetDatabaseAsync().GetAwaiter().GetResult();
        this.client = factory.CreateClient();
    }

    [Fact]
    public async Task GetUserProfile_ReturnsOk()
    {
        // Arrange — user profile is seeded in the factory

        // Act
        var response = await this.client.GetAsync("/api/core/users/me");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<UserProfileModel>();
        content.ShouldNotBeNull();
        content.Name.ShouldBe("Test User");
        content.Email.ShouldBe("test@example.com");
    }

    [Fact]
    public async Task UpdateUserProfile_ReturnsOk()
    {
        // Arrange — reset to clean state
        await this.factory.ResetDatabaseAsync();
        var updateModel = new UserProfileUpdateModel
        {
            Name = "Updated Name",
            Email = "updated@example.com"
        };

        // Act
        var response = await this.client.PutAsJsonAsync("/api/core/users/me", updateModel);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<UserProfileModel>();
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Updated Name");
        result.Email.ShouldBe("updated@example.com");

        // Verify via GET
        var getResponse = await this.client.GetAsync("/api/core/users/me");
        var profile = await getResponse.Content.ReadFromJsonAsync<UserProfileModel>();
        profile.Name.ShouldBe("Updated Name");
    }

    [Fact]
    public async Task UpdateUserProfile_WhenInvalid_ReturnsBadRequest()
    {
        // Arrange — empty name violates validation
        var updateModel = new UserProfileUpdateModel
        {
            Name = "",
            Email = "test@example.com"
        };

        // Act
        var response = await this.client.PutAsJsonAsync("/api/core/users/me", updateModel);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetUserPreferences_ReturnsOk()
    {
        // Arrange — profile with default preferences is seeded

        // Act
        var response = await this.client.GetAsync("/api/core/users/preferences");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var prefs = await response.Content.ReadFromJsonAsync<UnitPreferencesModel>();
        prefs.ShouldNotBeNull();
        prefs.TemperatureUnit.ShouldBe("Celsius");
        prefs.WindSpeedUnit.ShouldBe("Kmh");
    }

    [Fact]
    public async Task UpdateUserPreferences_ReturnsOk()
    {
        // Arrange
        await this.factory.ResetDatabaseAsync();
        var updateModel = new UserPreferencesUpdateModel
        {
            TemperatureUnit = "Fahrenheit",
            WindSpeedUnit = "Mph"
        };

        // Act
        var response = await this.client.PutAsJsonAsync("/api/core/users/preferences", updateModel);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<UnitPreferencesModel>();
        result.ShouldNotBeNull();
        result.TemperatureUnit.ShouldBe("Fahrenheit");
        result.WindSpeedUnit.ShouldBe("Mph");
    }

    [Fact]
    public async Task DeleteUser_ReturnsNoContent()
    {
        // Arrange
        await this.factory.ResetDatabaseAsync();

        // Act
        var response = await this.client.DeleteAsync("/api/core/users/me");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
