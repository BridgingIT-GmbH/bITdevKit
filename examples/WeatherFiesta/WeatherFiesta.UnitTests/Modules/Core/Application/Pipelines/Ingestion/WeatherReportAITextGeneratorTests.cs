// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.UnitTests.Modules.Core.WeatherReports;

using BridgingIT.DevKit.Examples.WeatherFiesta.Infrastructure;
using Microsoft.Extensions.AI;

/// <summary>
/// Unit tests for <see cref="WeatherReportAITextGenerator" />.
/// </summary>
public sealed class WeatherReportAITextGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_ValidChatResponse_ReturnsTrimmedSummary()
    {
        // Arrange
        var chatClient = Substitute.For<IChatClient>();
        chatClient.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, "  Clear and mild today.  ")));
        var sut = new WeatherReportAITextGenerator(chatClient, CreateOptions());

        // Act
        var result = await sut.GenerateAsync(CreateRequest());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("Clear and mild today.");
        await chatClient.Received(1).GetResponseAsync(
            Arg.Is<IEnumerable<ChatMessage>>(messages => messages.Count() == 2),
            Arg.Is<ChatOptions>(options => options.Temperature == 0.1f && options.TopP == 0.8f && options.MaxOutputTokens == 180),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("# Heading")]
    [InlineData("- bullet")]
    [InlineData("{ \"summary\": \"rain\" }")]
    public async Task GenerateAsync_InvalidChatResponse_ReturnsFailure(string responseText)
    {
        // Arrange
        var chatClient = Substitute.For<IChatClient>();
        chatClient.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));
        var sut = new WeatherReportAITextGenerator(chatClient, CreateOptions());

        // Act
        var result = await sut.GenerateAsync(CreateRequest());

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    private static IOptions<WeatherReportTextGenerationOptions> CreateOptions()
    {
        return Options.Create(new WeatherReportTextGenerationOptions
        {
            Endpoint = "http://localhost:8080/v1",
            ApiKey = "no-key",
            Model = "local-weather-summary",
            Temperature = 0.1f,
            TopP = 0.8f,
            MaxOutputTokens = 180
        });
    }

    private static WeatherReportTextGenerationRequest CreateRequest()
    {
        return new WeatherReportTextGenerationRequest(
            "Berlin",
            WeatherReportType.Today,
            new DateTime(2026, 06, 09, 00, 00, 00, DateTimeKind.Utc),
            new DateTime(2026, 06, 10, 00, 00, 00, DateTimeKind.Utc),
            new DateOnly(2026, 06, 09),
            new DateOnly(2026, 06, 10),
            [
                new WeatherForecastDayInput(
                    new DateOnly(2026, 06, 09),
                    2,
                    16m,
                    24m,
                    15m,
                    25m,
                    1.2m,
                    20,
                    18m,
                    31m,
                    225,
                    5m,
                    18000,
                    54000,
                    [])
            ],
            new CurrentWeatherInput(21m, 20m, 55, 2, 14m, 180, 28m, 0.1m, 40, 1015m, DateTime.UtcNow));
    }
}