// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.IntegrationTests.Presentation;

/// <summary>
/// Integration tests for subscription management endpoints.
/// Uses real IRequester pipeline with InMemory EF Core.
/// </summary>
[Trait("Category", "Integration")]
[Collection(WeatherFiestaTestCollection.Name)]
public class SubscriptionEndpointsTests
{
    private readonly HttpClient client;
    private readonly WeatherFiestaApplicationFactory factory;

    public SubscriptionEndpointsTests(WeatherFiestaApplicationFactory factory, ITestOutputHelper output)
    {
        this.factory = factory;
        factory.SetOutput(output);
        factory.ResetDatabaseAsync().GetAwaiter().GetResult();
        this.client = factory.CreateClient();
    }

    [Fact]
    public async Task GetUserSubscription_ReturnsOk()
    {
        // Arrange — handler finds seeded Free subscription

        // Act
        var response = await this.client.GetAsync("/api/core/users/subscription");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<UserSubscriptionModel>();
        content.ShouldNotBeNull();
        content.Plan.ShouldBe("Free");
        content.Status.ShouldBe("Active");
        content.UserId.ShouldBe(TestData.TestUserId);
        content.ActivePeriod.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AdminListSubscriptions_ReturnsOk()
    {
        // Arrange — admin lists all subscriptions

        // Act
        var response = await this.client.GetAsync("/api/core/admin/subscriptions");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var subscriptions = await response.Content.ReadFromJsonAsync<List<UserSubscriptionModel>>();
        subscriptions.ShouldNotBeNull();
        subscriptions.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task AdminGetUserSubscription_ReturnsOk()
    {
        // Arrange
        var userId = TestData.TestUserId;

        // Act
        var response = await this.client.GetAsync($"/api/core/admin/subscriptions/{userId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var subscription = await response.Content.ReadFromJsonAsync<UserSubscriptionModel>();
        subscription.ShouldNotBeNull();
        subscription.UserId.ShouldBe(userId);
        subscription.Plan.ShouldBe("Free");
        subscription.ActivePeriod.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AdminUpdateUserSubscription_ReturnsOk()
    {
        // Arrange
        await this.factory.ResetDatabaseAsync();
        var userId = TestData.TestUserId;
        var updateModel = new AdminSubscriptionUpdateModel
        {
            Plan = "Pro",
            Status = "Active",
            BillingCycle = "Monthly"
        };

        // Act
        var response = await this.client.PutAsJsonAsync(
            $"/api/core/admin/subscriptions/{userId}", updateModel);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<UserSubscriptionModel>();
        result.ShouldNotBeNull();
        result.Plan.ShouldBe("Pro");
        result.BillingCycle.ShouldBe("Monthly");
    }
}
