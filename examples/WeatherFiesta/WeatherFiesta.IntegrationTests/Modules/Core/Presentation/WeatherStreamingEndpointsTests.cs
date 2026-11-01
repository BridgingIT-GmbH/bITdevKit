// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.IntegrationTests.Presentation;

using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;

/// <summary>Exercises stored-weather streaming through real application and Active Entity operations.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~WeatherStreamingEndpointsTests</code></example>
[Collection(WeatherFiestaTestCollection.Name)]
public sealed class WeatherStreamingEndpointsTests(WeatherFiestaApplicationFactory factory, ITestOutputHelper output) : IAsyncLifetime
{
    private HttpClient client;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        factory.SetOutput(output);
        await factory.ResetDatabaseAsync();
        factory.CurrentUserAccessor.UserId.Returns(TestData.TestUserId);
        factory.WeatherAgent.ClearReceivedCalls();
        this.client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    }

    /// <inheritdoc/>
    public Task DisposeAsync()
    {
        this.client.Dispose();
        factory.CurrentUserAccessor.UserId.Returns(TestData.TestUserId);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task WeatherStream_MoreThanTwoBatches_PreservesStableOrderAndStoredStaleness()
    {
        string[] expected;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            await context.UserCities.ExecuteDeleteAsync();
            for (var index = 0; index < 205; index++)
            {
                var city = City.Create($"NDJSON city {index:000}", "Fictional country", "GB", "Europe/London", Location.Create(51m + (index / 1000m), 1m).Value, 9000000000L + index, 10m);
                context.Cities.Add(city);
                context.UserCities.Add(UserCity.Create(TestData.TestUserId, city.Id, displayOrder: index / 2));
                var weather = CurrentWeather.Create(city.Id);
                weather.Temperature = 15m + (index / 100m);
                weather.RetrievedAt = DateTime.UtcNow.AddDays(-2);
                context.CurrentWeathers.Add(weather);
            }

            await context.SaveChangesAsync();
            expected = (await context.UserCities.AsNoTracking().OrderBy(e => e.DisplayOrder).ThenBy(e => e.Id).Select(e => e.Id).ToArrayAsync()).Select(id => id.ToString()).ToArray();
        }

        var models = await this.ReadAllAsync();
        models.Count.ShouldBe(205);
        models.Select(m => m.Id).ShouldBe(expected);
        models.Select(m => m.Id).Distinct().Count().ShouldBe(205);
        models.All(m => m.UserId == TestData.TestUserId).ShouldBeTrue();
        models.All(m => m.City is not null && m.CurrentWeather is not null).ShouldBeTrue();
        models.All(m => m.StaleDataWarning && m.CurrentWeather.StaleDataWarning).ShouldBeTrue();
        factory.WeatherAgent.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task WeatherStream_CurrentUser_ExcludesOtherUsersSubscriptions()
    {
        var otherUser = "ndjson-other-" + Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            context.UserCities.Add(UserCity.Create(otherUser, CityId.Create(TestData.ParisCityGuid), displayOrder: 1));
            await context.SaveChangesAsync();
        }

        var first = await this.ReadAllAsync();
        first.Count.ShouldBe(1);
        first[0].CityId.ShouldBe(TestData.LondonCityGuid.ToString());
        factory.CurrentUserAccessor.UserId.Returns(otherUser);
        var second = await this.ReadAllAsync();
        second.Count.ShouldBe(1);
        second[0].UserId.ShouldBe(otherUser);
        second[0].CityId.ShouldBe(TestData.ParisCityGuid.ToString());
        factory.WeatherAgent.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task WeatherStream_ZeroSubscriptions_ProducesValidEmptyRawResponse()
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<CoreDbContext>().UserCities.ExecuteDeleteAsync();
        }

        (await this.ReadAllAsync()).ShouldBeEmpty();
        factory.WeatherAgent.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task WeatherStream_CallerRecordLimit_RejectsWithoutRaisingClientLimit()
    {
        var opened = await this.client.GetNdjsonAsync<UserCityModel>("/api/core/cities/weather/stream", new() { ReaderOptions = new() { MaxRecordBytes = 32 } });
        opened.IsSuccess.ShouldBeTrue();
        await using var response = opened.Value;
        var error = await Should.ThrowAsync<NdjsonStreamException>(async () => { await foreach (var unused in response.ReadAsync()) { } });
        error.ReasonCode.ShouldBe("stream_record_too_large");
    }

    [Fact]
    public async Task WeatherStream_CallerCancellation_RemainsDistinctFromSuccessfulCompletion()
    {
        var opened = await this.client.GetNdjsonAsync<UserCityModel>("/api/core/cities/weather/stream");
        await using var response = opened.Value;
        using var cancellation = new CancellationTokenSource();
        await using var items = response.ReadAsync(cancellation.Token).GetAsyncEnumerator();
        (await items.MoveNextAsync()).ShouldBeTrue();
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => items.MoveNextAsync().AsTask());
    }

    [Theory]
    [InlineData("/api/core/cities")]
    [InlineData("/api/core/cities/export")]
    public async Task ExistingEndpoints_AfterStreamingAddition_RetainJsonResponse(string route)
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            var subscription = await context.UserSubscriptions.SingleAsync(s => s.UserId == TestData.TestUserId);
            subscription.ChangePlan(SubscriptionPlan.Basic, SubscriptionBillingCycle.Monthly);
            await context.SaveChangesAsync();
        }

        using var response = await this.client.GetAsync(route);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType.MediaType.ShouldBe("application/json");
        response.Headers.Contains("X-Ndjson-Mode").ShouldBeFalse();
    }

    private async Task<List<UserCityModel>> ReadAllAsync()
    {
        var opened = await this.client.GetNdjsonAsync<UserCityModel>("/api/core/cities/weather/stream");
        opened.IsSuccess.ShouldBeTrue();
        await using var response = opened.Value;
        var models = new List<UserCityModel>();
        await foreach (var model in response.ReadAsync()) { models.Add(model); }

        return models;
    }
}
