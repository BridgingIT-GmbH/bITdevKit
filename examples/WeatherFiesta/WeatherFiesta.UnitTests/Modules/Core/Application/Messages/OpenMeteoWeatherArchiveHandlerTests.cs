// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.UnitTests.Modules.Core.Application.Messages;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;
using Microsoft.Extensions.Logging;

/// <summary>
/// Unit tests for <see cref="OpenMeteoWeatherArchiveHandler" />.
/// </summary>
public class OpenMeteoWeatherArchiveHandlerTests
{
    [Fact]
    public async Task Handle_ValidMessage_UpsertsArchiveDocument()
    {
        // Arrange
        var documentStoreClient = Substitute.For<IDocumentStoreClient<OpenMeteoWeatherArchiveDocument>>();
        var logger = Substitute.For<ILogger<OpenMeteoWeatherArchiveHandler>>();
        var sut = new OpenMeteoWeatherArchiveHandler(documentStoreClient, logger);
        var cityId = Guid.Parse("f9c6db0b-2f82-4c74-b90c-30eec42f8ef1");
        var retrievedAt = new DateTimeOffset(2026, 7, 7, 12, 34, 56, 789, TimeSpan.Zero);
        var ingestionResult = new WeatherIngestionResult
        {
            ProviderName = "openmeteo",
            ProviderRetrievedAt = retrievedAt,
            CurrentWeather = new CurrentWeatherData
            {
                TemperatureCelsius = 19.5,
                RetrievedAt = retrievedAt.UtcDateTime
            },
            Forecasts =
            [
                new ForecastData
                {
                    ForecastDate = retrievedAt.UtcDateTime.Date,
                    TemperatureMaxCelsius = 24,
                    TemperatureMinCelsius = 15
                }
            ]
        };
        var message = new OpenMeteoWeatherArchiveMessage
        {
            MessageId = "message-123",
            CityId = cityId.ToString("D"),
            CityName = "London",
            CountryCode = "GB",
            ProviderName = "openmeteo",
            RetrievedAt = retrievedAt,
            WeatherIngestionResult = ingestionResult
        };

        documentStoreClient.UpsertResultAsync(
                Arg.Any<DocumentKey>(),
                Arg.Any<OpenMeteoWeatherArchiveDocument>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        await sut.Handle(message, CancellationToken.None);

        // Assert
        await documentStoreClient.Received(1).UpsertResultAsync(
            Arg.Is<DocumentKey>(key =>
                key.PartitionKey == "archive/openmeteo/weather" &&
                key.RowKey == $"{cityId:N}/2026/07/07/123456789-message-123.json"),
            Arg.Is<OpenMeteoWeatherArchiveDocument>(document =>
                document.CityId == cityId.ToString("D") &&
                document.CityName == "London" &&
                document.CountryCode == "GB" &&
                document.ProviderName == "openmeteo" &&
                document.RetrievedAt == retrievedAt &&
                document.WeatherIngestionResult == ingestionResult),
            Arg.Any<CancellationToken>());
    }
}
