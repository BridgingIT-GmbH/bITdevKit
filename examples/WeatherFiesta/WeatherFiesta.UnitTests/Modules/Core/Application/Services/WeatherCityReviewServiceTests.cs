// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.UnitTests.Modules.Core.Services;

/// <summary>Verifies bounded real-handler composition, repeated capture, and optional profiling for city reviews.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~WeatherCityReviewServiceTests</code></example>
public sealed class WeatherCityReviewServiceTests
{
    /// <summary>Omitted and disabled capture keep the same review result and exactly bounded handler calls.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ReviewAsync_OptionalCapture_PreservesResultsAndAggregatesRepetitions(int mode)
    {
        var requester = CreateRequester();
        var services = new ServiceCollection();
        services.AddLogging();
        if (mode > 0)
        {
            services.AddProfiling(options => options.Enabled(mode == 2)).WithOperationProfiling();
        }

        using var provider = services.BuildServiceProvider();
        var writer = provider.GetService<OperationProfilingWriterService>();
        if (writer is not null)
        {
            await writer.TickAsync();
        }

        var sut = new WeatherCityReviewService(
            requester,
            provider.GetService<IOperationProfiler>()
        );
        var result = await sut.ReviewAsync(2, 3);

        result.IsSuccess.ShouldBeTrue();
        result.Value.CityCount.ShouldBe(2);
        result.Value.WeatherReadCount.ShouldBe(6);
        result.Value.MissingWeatherCount.ShouldBe(0);
        result.Value.AverageTemperature.ShouldBe(12m);
        await requester
            .Received(6)
            .SendAsync(
                Arg.Any<AdminCityWeatherQuery>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
        if (mode != 2)
        {
            result.Value.OperationId.ShouldBeNull();
            return;
        }

        await writer.TickAsync();
        var record = (
            await provider
                .GetRequiredService<IOperationProfilingQueryService>()
                .FindAsync(result.Value.OperationId.Value)
        ).Value;
        record.Key.ShouldBe("weather:city-review");
        record.Kind.ShouldBe("Service");
        record.Http.ShouldBeNull();
        record.Outcome.ShouldBe(OperationProfilingOutcome.Completed);
        record
            .Segments.Single(segment => segment.Key == "ReadWeather")
            .Statistics.Count.ShouldBe(6);
        record.Segments.Single(segment => segment.Key == "LoadCities").Statistics.Count.ShouldBe(1);
        record.Measurements.ShouldContain(measurement => measurement.Key == "weatherReads");
    }

    /// <summary>Invalid limits perform no handler work.</summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(11, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 6)]
    public async Task ReviewAsync_InvalidLimits_RejectBeforeLoading(int cityLimit, int repetitions)
    {
        var requester = CreateRequester();
        var sut = new WeatherCityReviewService(requester);

        (await sut.ReviewAsync(cityLimit, repetitions)).IsFailure.ShouldBeTrue();
        requester.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Missing weather is visible in segment failures and the successful partial review summary.</summary>
    [Fact]
    public async Task ReviewAsync_MissingWeather_ClassifiesReadFailures()
    {
        var requester = CreateRequester();
        requester
            .SendAsync(
                Arg.Any<AdminCityWeatherQuery>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(Result<CurrentWeatherModel>.Failure("Weather absent"));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProfiling(options => options.Enabled()).WithOperationProfiling();
        using var provider = services.BuildServiceProvider();
        var writer = provider.GetRequiredService<OperationProfilingWriterService>();
        await writer.TickAsync();
        var sut = new WeatherCityReviewService(
            requester,
            provider.GetRequiredService<IOperationProfiler>()
        );

        var result = await sut.ReviewAsync(2, 2);
        await writer.TickAsync();

        result.IsSuccess.ShouldBeTrue();
        result.Value.MissingWeatherCount.ShouldBe(4);
        result.Value.AverageTemperature.ShouldBeNull();
        var record = (
            await provider
                .GetRequiredService<IOperationProfilingQueryService>()
                .FindAsync(result.Value.OperationId.Value)
        ).Value;
        record.Outcome.ShouldBe(OperationProfilingOutcome.Completed);
        record
            .Segments.Single(segment => segment.Key == "ReadWeather")
            .Outcomes.ShouldContain(outcome =>
                outcome.Outcome == ProfilingSegmentOutcome.Failed && outcome.Statistics.Count == 4
            );
    }

    /// <summary>A failed city list returns its domain errors and stops weather reads.</summary>
    [Fact]
    public async Task ReviewAsync_LoadFailure_ReturnsFailureWithoutWeatherReads()
    {
        var requester = CreateRequester();
        requester
            .SendAsync(Arg.Any<AdminCitiesQuery>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Result<List<AdminCityModel>>.Failure("Read failed"));
        var sut = new WeatherCityReviewService(requester);

        (await sut.ReviewAsync()).IsFailure.ShouldBeTrue();
        await requester
            .DidNotReceive()
            .SendAsync(
                Arg.Any<AdminCityWeatherQuery>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    /// <summary>Cancellation reaches the original token and prevents weather reads.</summary>
    [Fact]
    public async Task ReviewAsync_CanceledToken_StopsLoop()
    {
        var requester = CreateRequester();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var sut = new WeatherCityReviewService(requester);

        await Should.ThrowAsync<OperationCanceledException>(() =>
            sut.ReviewAsync(cancellationToken: cancellation.Token)
        );
        await requester
            .DidNotReceive()
            .SendAsync(
                Arg.Any<AdminCityWeatherQuery>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    /// <summary>Faulty optional observers cannot replace the successful application work.</summary>
    [Fact]
    public async Task ReviewAsync_ObserverFailure_PreservesBusinessResult()
    {
        var requester = CreateRequester();
        var profiler = Substitute.For<IOperationProfiler>();
        profiler.Current.Returns(_ => throw new InvalidOperationException("Observer failed"));
        var sut = new WeatherCityReviewService(requester, profiler);

        var result = await sut.ReviewAsync(1);

        result.IsSuccess.ShouldBeTrue();
        result.Value.WeatherReadCount.ShouldBe(1);
        result.Value.OperationId.ShouldBeNull();
    }

    private static IRequester CreateRequester()
    {
        var requester = Substitute.For<IRequester>();
        requester
            .SendAsync(Arg.Any<AdminCitiesQuery>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(
                Result<List<AdminCityModel>>.Success([
                    new() { Id = Guid.NewGuid().ToString(), Name = "Paris" },
                    new() { Id = Guid.NewGuid().ToString(), Name = "London" },
                ])
            );
        requester
            .SendAsync(
                Arg.Any<AdminCityWeatherQuery>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(Result<CurrentWeatherModel>.Success(new() { Temperature = 12m }));
        return requester;
    }
}
