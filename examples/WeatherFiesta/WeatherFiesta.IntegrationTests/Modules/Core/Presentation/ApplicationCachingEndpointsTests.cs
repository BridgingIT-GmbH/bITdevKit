// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.IntegrationTests.Presentation;

using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.Configuration;

/// <summary>Exercises the production geocoding cache and authorized cache dashboard over the module's real SQL Server schema.</summary>
/// <param name="factory">The existing isolated SQL Server and mocked external geocoding fixture.</param>
/// <param name="output">Current test logging.</param>
/// <example><code>dotnet test --filter FullyQualifiedName~ApplicationCachingEndpointsTests</code></example>
[Trait("Category", "Integration")]
[Collection(WeatherFiestaTestCollection.Name)]
public sealed class ApplicationCachingEndpointsTests(WeatherFiestaApplicationFactory factory, ITestOutputHelper output) : IAsyncLifetime
{
    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        factory.SetOutput(output);
        Rule.Settings.Logger = new RuleLogger(factory.Services.GetRequiredService<ILogger<RuleLogger>>());
        await factory.ResetDatabaseAsync();
    }

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        ActiveEntityConfigurator.SetGlobalServiceProvider(factory.Services);
        Rule.Settings.Logger = new RuleLogger(factory.Services.GetRequiredService<ILogger<RuleLogger>>());
        return Task.CompletedTask;
    }

    /// <summary>Repeated normalized HTTP searches reuse one retained SQL-backed result and invoke the external source only once.</summary>
    [Fact]
    public async Task CitySuggestions_RepeatedNormalizedSearch_UsesSharedStoreCollection()
    {
        factory.GeocodingClient.SearchCitiesAsync("Haarlem", "NL", Arg.Any<CancellationToken>()).Returns(new GeocodingResponseModel
        {
            Results = [new() { Name = "Haarlem", CountryCode = "NL", Latitude = 52.38m, Longitude = 4.64m }]
        });
        await using var host = this.CreateSqlCacheHost();
        using var client = host.CreateClient();
        host.Services.GetRequiredService<IDatabaseReadyService>().SetReady(nameof(CoreDbContext));

        var first = await client.GetFromJsonAsync<List<CitySuggestionModel>>("/api/core/cities/suggestions?search=Haarlem&countryCode=nl");
        var second = await client.GetFromJsonAsync<List<CitySuggestionModel>>("/api/core/cities/suggestions?search=%20haarlem%20&countryCode=NL");

        first.Single().Name.ShouldBe("Haarlem");
        second.Single().Name.ShouldBe("Haarlem");
        await factory.GeocodingClient.Received(1).SearchCitiesAsync("Haarlem", "NL", Arg.Any<CancellationToken>());
        var binding = host.Services.GetRequiredService<CacheCatalog>().Collections[CitySuggestionService.CollectionName];
        binding.Provider.Options.Kind.ShouldBe("EntityFramework");
        binding.Provider.Options.Engine.ShouldBe("SqlServer");
        binding.Options.Loading.CoordinationScope.ShouldBe(CacheCoordinationScope.SharedStore);
        await using var context = host.Services.GetRequiredService<IDbContextFactory<CoreDbContext>>().CreateDbContext();
        (await context.CacheEntries.CountAsync()).ShouldBe(1);
    }

    /// <summary>Cache pages retain administrator access, and finalized collection settings honor the host configuration section.</summary>
    [Fact]
    public async Task Dashboard_CachePages_RequireAdministratorAndApplyCollectionConfiguration()
    {
        await using var host = this.CreateSqlCacheHost(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Caching:Application:Collections:city-suggestions:Expiration:AbsoluteTtl"] = "00:07:00"
            }));
            builder.ConfigureServices(services =>
            {
                services.AddDashboard(options => options.Enabled().Authorize(auth => auth
                    .UseExistingScheme("WeatherCachingTest").RequireRole(Role.Administrators)));
                services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, ProfilingDashboardAuthenticationHandler>("WeatherCachingTest", _ => { });
            });
        });
        using var client = host.CreateClient(new() { AllowAutoRedirect = false });
        host.Services.GetRequiredService<IDatabaseReadyService>().SetReady(nameof(CoreDbContext));
        var catalog = host.Services.GetRequiredService<CacheCatalog>();
        catalog.Collections[CitySuggestionService.CollectionName].Options.Expiration.AbsoluteTtl.ShouldBe(TimeSpan.FromMinutes(7));
        catalog.Integrations.ContainsKey("Dashboard").ShouldBeTrue();
        catalog.Integrations.ContainsKey("Endpoints").ShouldBeFalse();
        var stored = await host.Services.GetRequiredService<IApplicationCache>()
            .GetCollection<CitySuggestionModel[]>(CitySuggestionService.CollectionName).SetAsync(new("NL:Dashboard"), []);
        stored.IsSuccess.ShouldBeTrue();

        foreach (var role in new[] { "", "user", "admin" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/_bdk/dashboard/caching/collections/city-suggestions/entries");
            if (role.Length > 0) { request.Headers.Add("X-Test-Dashboard-Role", role); }

            using var response = await client.SendAsync(request);
            if (role == "user")
            {
                // Dashboard HTML denial uses its access-denied page rather than the host application's page.
                response.StatusCode.ShouldBe(HttpStatusCode.Found);
                response.Headers.Location.OriginalString.ShouldStartWith("/_bdk/dashboard/access-denied?");
                using var denied = await client.GetAsync(response.Headers.Location);
                denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
                continue;
            }

            response.StatusCode.ShouldBe(role switch
            {
                "admin" => HttpStatusCode.OK,
                _ => HttpStatusCode.Unauthorized
            });
            var html = await response.Content.ReadAsStringAsync();
            if (role == "admin")
            {
                html.ShouldContain("NL:Dashboard");
                html.ShouldNotContain("[redacted]");
            }
            else { html.ShouldNotContain("NL:Dashboard"); }
        }
    }

    /// <summary>An external source failure maps to a gateway failure and a later successful source is not hidden by an error entry.</summary>
    [Fact]
    public async Task CitySuggestions_SourceFailure_ReturnsBadGatewayAndAllowsRecovery()
    {
        factory.GeocodingClient.SearchCitiesAsync("Breda", "NL", Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<GeocodingResponseModel>(new HttpRequestException("The geocoding source is unavailable.")));
        await using var host = this.CreateSqlCacheHost();
        using var client = host.CreateClient();
        host.Services.GetRequiredService<IDatabaseReadyService>().SetReady(nameof(CoreDbContext));

        using var failed = await client.GetAsync("/api/core/cities/suggestions?search=Breda&countryCode=NL");
        failed.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        // A delivered failure does not end still-occupied ownership/fence cleanup.
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var tasks = host.Services.GetRequiredService<CacheRuntimeTasks>();
        while (tasks.Count > 0) { await Task.Delay(1, deadline.Token); }

        var retained = await host.Services.GetRequiredService<IApplicationCache>()
            .GetCollection<CitySuggestionModel[]>(CitySuggestionService.CollectionName).GetAsync("NL:Breda");
        retained.IsSuccess.ShouldBeTrue();
        retained.Value.Found.ShouldBeFalse();
        retained.Value.IsNotFound.ShouldBeFalse();
        factory.GeocodingClient.SearchCitiesAsync("Breda", "NL", Arg.Any<CancellationToken>()).Returns(new GeocodingResponseModel
        {
            Results = [new() { Name = "Breda", CountryCode = "NL", Latitude = 51.59m, Longitude = 4.77m }]
        });
        using var recoveredResponse = await client.GetAsync("/api/core/cities/suggestions?search=Breda&countryCode=NL");
        recoveredResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await recoveredResponse.Content.ReadAsStringAsync());
        var recovered = await recoveredResponse.Content.ReadFromJsonAsync<List<CitySuggestionModel>>();

        recovered.Single().Name.ShouldBe("Breda");
    }

    private WebApplicationFactory<Program> CreateSqlCacheHost(Action<IWebHostBuilder> configure = null) => factory.WithWebHostBuilder(builder =>
    {
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Caching:Application:Providers:core:Kind"] = "EntityFramework",
            ["Caching:Application:Providers:core:Engine"] = "SqlServer",
            ["Caching:Application:Collections:city-suggestions:Loading:CoordinationScope"] = "SharedStore"
        }));
        configure?.Invoke(builder);
    });
}