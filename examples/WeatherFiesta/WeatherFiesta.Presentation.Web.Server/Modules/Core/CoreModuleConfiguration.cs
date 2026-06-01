// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Presentation.Web.Server.Modules.Core;

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

    /// <summary>Gets or sets the cron expression for the weather ingestion job.</summary>
    public string IngestionCron { get; set; } = "0 */30 * * * ?";

    /// <summary>Gets or sets the number of forecast days to retrieve.</summary>
    public int ForecastDays { get; set; } = 16;

    /// <summary>Gets or sets the minimum query length for geocoding search.</summary>
    public int GeocodingMinQueryLength { get; set; } = 3;

    /// <summary>Gets or sets the maximum number of cities for comparison.</summary>
    public int ComparisonMaxCities { get; set; } = 10;

    /// <summary>Gets or sets the admin role name for authorization.</summary>
    public string AdminRoleName { get; set; } = "CoreAdmin";

    /// <summary>Gets or sets the default subscription plan for new users.</summary>
    public string DefaultPlan { get; set; } = "Free";

    /// <summary>Gets or sets the Open-Meteo API options.</summary>
    public OpenMeteoOptions OpenMeteo { get; set; } = new();

    /// <summary>
    /// FluentValidation validator for <see cref="CoreModuleConfiguration"/>.
    /// </summary>
    public class Validator : AbstractValidator<CoreModuleConfiguration>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Validator"/> class.
        /// </summary>
        public Validator()
        {
            RuleFor(x => x.ConnectionStrings)
                .NotNull().NotEmpty().Must(c => c.ContainsKey("Default"))
                .WithMessage("Connection string with name 'Default' is required");

            RuleFor(x => x.StaleThresholdMinutes)
                .GreaterThan(0)
                .WithMessage("StaleThresholdMinutes must be greater than 0");

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
