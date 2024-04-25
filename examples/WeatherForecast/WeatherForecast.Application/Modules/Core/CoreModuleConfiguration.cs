// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Application.Modules.Core;

using FluentValidation;
using System.ComponentModel.DataAnnotations;

public class CoreModuleConfiguration
{
    [Required]
    [Url]
    public string OpenWeatherUrl { get; set; }

    [Required]
    public string OpenWeatherApiKey { get; set; }

    public IReadOnlyDictionary<string, string> ConnectionStrings { get; set; }

    public class Validator : AbstractValidator<CoreModuleConfiguration>
    {
        public Validator()
        {
            this.RuleFor(c => c.ConnectionStrings)
                .NotNull().NotEmpty()
                .Must(c => c.ContainsKey("Default"))
                .WithMessage("Connection string with name 'default' is required");

            this.RuleFor(c => c.OpenWeatherUrl)
                .NotNull().NotEmpty()
                .WithMessage("OpenWeatherUrl cannot be null or empty");

            this.RuleFor(c => c.OpenWeatherApiKey)
                .NotNull().NotEmpty()
                .WithMessage("OpenWeatherApiKey cannot be null or empty");
        }
    }
}