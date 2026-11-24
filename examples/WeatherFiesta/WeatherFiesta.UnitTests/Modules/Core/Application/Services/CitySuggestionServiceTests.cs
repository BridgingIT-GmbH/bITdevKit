// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.UnitTests.Modules.Core.Services;

using BridgingIT.DevKit.Application.Caching;

/// <summary>Verifies reusable geocoding results, source ownership, and independent mutable response copies.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~CitySuggestionServiceTests</code></example>
public sealed class CitySuggestionServiceTests
{
    /// <summary>Normalized repeated searches share one source while response mutations remain local.</summary>
    [Fact]
    public async Task SearchAsync_NormalizedSearch_ReusesSourceAndCopiesResponseModels()
    {
        var client = Substitute.For<IWeatherGeocodingClient>();
        client.SearchCitiesAsync("Haarlem", "NL", Arg.Any<CancellationToken>()).Returns(Response("Haarlem", "NL"));
        await using var services = CreateServices(client);
        var sut = new CitySuggestionService(services.GetRequiredService<IApplicationCache>());

        var first = await sut.SearchAsync(" Haarlem ", "nl");
        first.IsSuccess.ShouldBeTrue();
        first.Value[0].Name = "Changed by caller";
        first.Value.Clear();
        var second = await sut.SearchAsync("haarlem", " NL ");

        second.IsSuccess.ShouldBeTrue();
        second.Value.Single().Name.ShouldBe("Haarlem");
        await client.Received(1).SearchCitiesAsync("Haarlem", "NL", Arg.Any<CancellationToken>());
    }

    /// <summary>The optional country is part of the cached source identity.</summary>
    [Fact]
    public async Task SearchAsync_DifferentCountries_LoadDistinctResults()
    {
        var client = Substitute.For<IWeatherGeocodingClient>();
        client.SearchCitiesAsync("Paris", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Response("Paris", call.ArgAt<string>(1)));
        await using var services = CreateServices(client);
        var sut = new CitySuggestionService(services.GetRequiredService<IApplicationCache>());

        var france = await sut.SearchAsync("Paris", "fr");
        var unitedStates = await sut.SearchAsync("Paris", "us");
        var unrestricted = await sut.SearchAsync("Paris");

        france.Value.Single().CountryCode.ShouldBe("FR");
        unitedStates.Value.Single().CountryCode.ShouldBe("US");
        unrestricted.Value.Single().CountryCode.ShouldBeNull();
        await client.Received(3).SearchCitiesAsync("Paris", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>A successful empty response is cached as a positive empty result.</summary>
    [Fact]
    public async Task SearchAsync_EmptyResults_RetainsSuccessfulEmptyArray()
    {
        var client = Substitute.For<IWeatherGeocodingClient>();
        client.SearchCitiesAsync("NoSuchCity", null, Arg.Any<CancellationToken>()).Returns(new GeocodingResponseModel());
        await using var services = CreateServices(client);
        var sut = new CitySuggestionService(services.GetRequiredService<IApplicationCache>());

        (await sut.SearchAsync("NoSuchCity")).Value.ShouldBeEmpty();
        (await sut.SearchAsync("NoSuchCity")).Value.ShouldBeEmpty();

        await client.Received(1).SearchCitiesAsync("NoSuchCity", null, Arg.Any<CancellationToken>());
    }

    /// <summary>An unavailable source remains a failure and a later request executes it again.</summary>
    [Fact]
    public async Task SearchAsync_SourceFailure_DoesNotRetainFailure()
    {
        var client = Substitute.For<IWeatherGeocodingClient>();
        client.SearchCitiesAsync("Leiden", "NL", Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<GeocodingResponseModel>(new HttpRequestException("The geocoding source is unavailable.")));
        await using var services = CreateServices(client);
        var sut = new CitySuggestionService(services.GetRequiredService<IApplicationCache>());

        var failed = await sut.SearchAsync("Leiden", "NL");
        client.SearchCitiesAsync("Leiden", "NL", Arg.Any<CancellationToken>()).Returns(Response("Leiden", "NL"));
        var recovered = await sut.SearchAsync("Leiden", "NL");

        failed.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.SourceFailure);
        recovered.Value.Single().Name.ShouldBe("Leiden");
        await client.Received(2).SearchCitiesAsync("Leiden", "NL", Arg.Any<CancellationToken>());
    }

    /// <summary>Concurrent interested callers execute one source and each receive their own response graph.</summary>
    [Fact]
    public async Task SearchAsync_ConcurrentCallers_CoalescesSourceWithoutSharingMutableResponse()
    {
        var client = Substitute.For<IWeatherGeocodingClient>();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<GeocodingResponseModel>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.SearchCitiesAsync("Delft", "NL", Arg.Any<CancellationToken>()).Returns(_ =>
        {
            entered.TrySetResult();
            return release.Task;
        });
        await using var services = CreateServices(client);
        var sut = new CitySuggestionService(services.GetRequiredService<IApplicationCache>());
        var pending = Enumerable.Range(0, 4).Select(_ => sut.SearchAsync("Delft", "NL")).ToArray();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var observations = services.GetRequiredService<CacheDiagnostics>().ForCollection(CitySuggestionService.CollectionName);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (observations.Snapshot().Counters.GetValueOrDefault(CacheObservationKind.Join) < 3)
            {
                await Task.Delay(1, deadline.Token);
            }
        }
        finally { release.TrySetResult(Response("Delft", "NL")); }

        var results = await Task.WhenAll(pending);
        results.All(result => result.IsSuccess).ShouldBeTrue();
        results[0].Value[0].Name = "Changed by caller";
        results.Skip(1).ShouldAllBe(result => result.Value[0].Name == "Delft");
        await client.Received(1).SearchCitiesAsync("Delft", "NL", Arg.Any<CancellationToken>());
    }

    /// <summary>Invalid source inputs start neither geocoding nor cache loading.</summary>
    /// <param name="search">The invalid city-search text.</param>
    /// <param name="countryCode">The optional invalid country code.</param>
    [Theory]
    [InlineData("ab", null)]
    [InlineData("   ", null)]
    [InlineData("Paris", "FRA")]
    [InlineData("Paris", "12")]
    public async Task SearchAsync_InvalidInput_RejectsBeforeSource(string search, string countryCode)
    {
        var client = Substitute.For<IWeatherGeocodingClient>();
        await using var services = CreateServices(client);
        var sut = new CitySuggestionService(services.GetRequiredService<IApplicationCache>());

        var result = await sut.SearchAsync(search, countryCode);

        result.Errors.OfType<ValidationError>().ShouldNotBeEmpty();
        client.ReceivedCalls().ShouldBeEmpty();
    }

    private static ServiceProvider CreateServices(IWeatherGeocodingClient client)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(client);
        services.AddApplicationCaching()
            .WithValueContract<CitySuggestionModel[]>(CitySuggestionService.CollectionName)
            .WithCollection<CitySuggestionModel[]>(CitySuggestionService.CollectionName, collection => collection
                .UseContract(CitySuggestionService.CollectionName));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static GeocodingResponseModel Response(string name, string countryCode) => new()
    {
        Results = [new() { Name = name, CountryCode = countryCode, Latitude = 52m, Longitude = 4m }]
    };
}