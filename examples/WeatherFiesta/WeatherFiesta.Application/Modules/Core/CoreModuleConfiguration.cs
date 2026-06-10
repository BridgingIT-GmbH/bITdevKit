// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Jobs;
using FluentValidation;

/// <summary>
/// Configuration options for the WeatherFiesta Core module.
/// </summary>
public class CoreModuleConfiguration
{
    /// <summary>Gets or sets the connection strings dictionary.</summary>
    public IReadOnlyDictionary<string, string> ConnectionStrings { get; set; }

    /// <summary>Gets or sets the startup delay for the seeder task.</summary>
    public string SeederTaskStartupDelay { get; set; } = "00:00:05";

    /// <summary>Gets or sets the stale data threshold in minutes.</summary>
    public int StaleThresholdMinutes { get; set; } = 60;

    /// <summary>Gets or sets the Jobs options.</summary>
    public WeatherJobOptions Jobs { get; set; } = new();

    /// <summary>Gets or sets the number of forecast days to retrieve.</summary>
    public int ForecastDays { get; set; } = 16;

    /// <summary>Gets or sets the minimum query length for geocoding search.</summary>
    public int GeocodingMinQueryLength { get; set; } = 3;

    /// <summary>Gets or sets the maximum number of cities for comparison.</summary>
    public int ComparisonMaxCities { get; set; } = 10;

    /// <summary>Gets or sets the default subscription plan for new users.</summary>
    public string DefaultPlan { get; set; } = "Free";

    /// <summary>Gets or sets the Open-Meteo API options.</summary>
    public OpenMeteoOptions OpenMeteo { get; set; } = new();

    /// <summary>Gets or sets the weather report text generation options.</summary>
    public WeatherReportTextGenerationOptions WeatherReportTextGeneration { get; set; } = new();

    /// <summary>
    /// FluentValidation validator for <see cref="CoreModuleConfiguration"/>.
    /// </summary>
    public class Validator : AbstractValidator<CoreModuleConfiguration>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Validator"/> class.
        /// </summary>
        public Validator(IJobCronEngine cronEngine)
        {
            RuleFor(x => x.ConnectionStrings)
                .NotNull().NotEmpty().Must(c => c.ContainsKey("Default"))
                .WithMessage("Connection string with name 'Default' is required");

            RuleFor(x => x.StaleThresholdMinutes)
                .GreaterThan(0)
                .WithMessage("StaleThresholdMinutes must be greater than 0");

            RuleFor(x => x.Jobs).SetValidator(new WeatherJobOptions.Validator(cronEngine));

            RuleFor(x => x.ForecastDays)
                .InclusiveBetween(1, 16)
                .WithMessage("ForecastDays must be between 1 and 16");

            RuleFor(x => x.ComparisonMaxCities)
                .InclusiveBetween(2, 50)
                .WithMessage("ComparisonMaxCities must be between 2 and 50");

            RuleFor(x => x.GeocodingMinQueryLength)
                .InclusiveBetween(1, 100)
                .WithMessage("GeocodingMinQueryLength must be between 1 and 100");

            RuleFor(x => x.OpenMeteo).SetValidator(new OpenMeteoOptions.Validator());
        }
    }
}

/// <summary>
/// Configuration options for WeatherFiesta Jobs.
/// </summary>
public class WeatherJobOptions
{
    /// <summary>Gets or sets the cron expression for the weather ingestion job.</summary>
    public string IngestionCron { get; set; } = CronExpressions.Every30Minutes;

    /// <summary>Gets or sets the cron expression for the weather data cleanup job.</summary>
    public string CleanupCron { get; set; } = CronExpressions.Every5Minutes;

    /// <summary>Gets or sets the number of days to keep weather data.</summary>
    public int CleanupRetentionDays { get; set; } = 31;

    /// <summary>
    /// FluentValidation validator for <see cref="WeatherJobOptions"/>.
    /// </summary>
    public class Validator : AbstractValidator<WeatherJobOptions>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Validator"/> class.
        /// </summary>
        public Validator(IJobCronEngine cronEngine)
        {
            RuleFor(x => x.IngestionCron)
                .NotNull().NotEmpty()
                .Must(value => cronEngine.Validate(value).IsSuccess)
                .WithMessage("Jobs.IngestionCron must be a valid Jobs cron expression (for example '*/30 * * * *').");

            RuleFor(x => x.CleanupCron)
                .NotNull().NotEmpty()
                .Must(value => cronEngine.Validate(value).IsSuccess)
                .WithMessage("Jobs.CleanupCron must be a valid Jobs cron expression (for example '0 2 * * *').");

            RuleFor(x => x.CleanupRetentionDays)
                .GreaterThan(0)
                .WithMessage("Jobs.CleanupRetentionDays must be greater than 0");
        }
    }
}

/// <summary>
/// Configuration options for the Open-Meteo API integration.
/// </summary>
public class OpenMeteoOptions
{
    /// <summary>Gets or sets the base URL for the Open-Meteo Geocoding API.</summary>
    public string GeocodingBaseUrl { get; set; } = "https://geocoding-api.open-meteo.com/v1";

    /// <summary>Gets or sets the base URL for the Open-Meteo Forecast API.</summary>
    public string ForecastBaseUrl { get; set; } = "https://api.open-meteo.com/v1/forecast";

    /// <summary>Gets or sets the base URL for the Open-Meteo city lookup API.</summary>
    public string LookupBaseUrl { get; set; } = "https://geocoding-api.open-meteo.com/v1/get";

    /// <summary>Gets or sets the HTTP request timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>Gets or sets the number of retry attempts for failed requests.</summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>Gets or sets the delay between retries in milliseconds.</summary>
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>Gets or sets the delay between consecutive API calls in milliseconds.</summary>
    public int InterCallDelayMs { get; set; } = 100;

    /// <summary>
    /// FluentValidation validator for <see cref="OpenMeteoOptions"/>.
    /// </summary>
    public class Validator : AbstractValidator<OpenMeteoOptions>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Validator"/> class.
        /// </summary>
        public Validator()
        {
            RuleFor(x => x.GeocodingBaseUrl).NotNull().NotEmpty();
            RuleFor(x => x.ForecastBaseUrl).NotNull().NotEmpty();
            RuleFor(x => x.LookupBaseUrl).NotNull().NotEmpty();
            RuleFor(x => x.TimeoutSeconds).GreaterThan(0);
            RuleFor(x => x.RetryCount).InclusiveBetween(0, 10);
            RuleFor(x => x.RetryDelayMs).GreaterThan(0);
            RuleFor(x => x.InterCallDelayMs).GreaterThanOrEqualTo(0);
        }
    }
}

/// <summary>
/// Options for local weather report text generation.
/// </summary>
public sealed class WeatherReportTextGenerationOptions
{
    /// <summary>Gets or sets the OpenAI-compatible endpoint.</summary>
    public string Endpoint { get; set; } = "http://localhost:8080/v1";

    /// <summary>Gets or sets the API key.</summary>
    public string ApiKey { get; set; } = "no-key";

    /// <summary>Gets or sets the model name.</summary>
    public string Model { get; set; } = "weather-summary";

    /// <summary>Gets or sets the sampling temperature.</summary>
    public float Temperature { get; set; } = 0.1f;

    /// <summary>Gets or sets the nucleus sampling value.</summary>
    public float TopP { get; set; } = 0.8f;

    /// <summary>Gets or sets the maximum output tokens.</summary>
    public int MaxOutputTokens { get; set; } = 180;
}