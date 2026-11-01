// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Examples.WeatherFiesta.IntegrationTests.Presentation;

using BridgingIT.DevKit.Application.Auditing;

/// <summary>Proves the sample host records Active Entity operations and exposes its protected audit dashboard.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~AuditingSampleTests</code></example>
[Collection(WeatherFiestaTestCollection.Name)]
public sealed class AuditingSampleTests(WeatherFiestaApplicationFactory factory, ITestOutputHelper output) : IAsyncLifetime
{
    private HttpClient client;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        factory.SetOutput(output);
        await factory.ResetDatabaseAsync();
        this.client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    }

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        this.client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CreateCity_RecordsReuseDecisionAndActiveEntityMutationsWithoutChangeHistory()
    {
        factory.GeocodingClient.SearchCityAsync("Audit city", "NL", Arg.Any<CancellationToken>()).Returns(new GeocodingResultModel
        {
            Name = "Audit city", Country = "Netherlands", CountryCode = "NL", Latitude = 52m, Longitude = 4m,
            TimeZone = "Europe/Amsterdam", ExternalId = 99887766
        });
        using var response = await this.client.PostAsJsonAsync("/api/core/cities", new CityCreateModel { Name = "Audit city", CountryCode = "NL" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var model = await response.Content.ReadFromJsonAsync<CityModel>();
        var id = Guid.Parse(response.Headers.GetValues("X-Audit-Id").Single());
        await using var reading = factory.Services.CreateAsyncScope();
        var found = await reading.ServiceProvider.GetRequiredService<IAuditQueryService>().FindAsync(id);
        found.IsSuccess.ShouldBeTrue();
        found.Value.Record.Outcome.ShouldBe(AuditOutcome.Succeeded);
        found.Value.Record.Actions.Any(action => action.Details.ContainsKey("city.reused")).ShouldBeTrue();
        found.Value.Record.Actions.Any(action => action.Feature == "ActiveEntity" && action.Resources.Any(resource => resource.Id == model.Id)).ShouldBeTrue();
        found.Value.Record.EntityChanges.ShouldBeEmpty();
        (await reading.ServiceProvider.GetRequiredService<CoreDbContext>().AuditRecords.AnyAsync(row => row.AuditId == id)).ShouldBeTrue();
    }

    [Fact]
    public async Task AuditingDashboard_RendersForAdministrator()
    {
        using var response = await this.client.GetAsync("/_bdk/dashboard/auditing");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Auditing");
    }
}
