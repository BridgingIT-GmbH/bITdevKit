// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Infrastructure;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using IWeatherReportTextGenerator = Application.Modules.Core.IWeatherReportTextGenerator;
using WeatherReportTextGenerationRequest = Application.Modules.Core.WeatherReportTextGenerationRequest;

/// <summary>
/// Generates weather report text through a local llama.cpp OpenAI-compatible endpoint.
/// </summary>
public sealed class WeatherReportAITextGenerator(
    IChatClient chatClient,
    IOptions<WeatherReportTextGenerationOptions> options) : IWeatherReportTextGenerator
{
    private readonly WeatherReportTextGenerationOptions options = options.Value;

    /// <inheritdoc />
    public async Task<Result<string>> GenerateAsync(
        WeatherReportTextGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        var prompt = WeatherReportPromptBuilder.Build(request);
        var response = await chatClient.GetResponseAsync(
            [
                new ChatMessage(ChatRole.System, prompt.System),
                new ChatMessage(ChatRole.User, prompt.User)
            ],
            new ChatOptions
            {
                Temperature = this.options.Temperature,
                TopP = this.options.TopP,
                MaxOutputTokens = this.options.MaxOutputTokens
            },
            cancellationToken);

        if (response is null || string.IsNullOrWhiteSpace(response.Text))
        {
            return Result<string>.Failure().WithError("Weather report text generation returned no or empty response.");
        }

        return Validate(response.Text);
    }

    private static Result<string> Validate(string text)
    {
        var summary = (text ?? string.Empty).Trim();
        if (summary.Length >= 2 && summary[0] == '"' && summary[^1] == '"')
        {
            summary = summary[1..^1].Trim();
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            return Result<string>.Failure().WithError("Weather report text generation returned empty output.");
        }

        if (summary.Length > 2000)
        {
            return Result<string>.Failure().WithError("Weather report text generation returned output longer than 2000 characters.");
        }

        if (summary.StartsWith('{') || summary.StartsWith('['))
        {
            return Result<string>.Failure().WithError("Weather report text generation returned structured output instead of plain text.");
        }

        var lines = summary.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (lines.Any(line => line.StartsWith('#') || line.StartsWith("- ") || line.StartsWith("* ")))
        {
            return Result<string>.Failure().WithError("Weather report text generation returned markdown instead of plain text.");
        }

        return Result<string>.Success(summary);
    }
}
