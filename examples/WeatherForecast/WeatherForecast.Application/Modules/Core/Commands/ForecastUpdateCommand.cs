// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Application.Modules.Core;

using BridgingIT.DevKit.Application.Commands;
using BridgingIT.DevKit.Examples.WeatherForecast.Domain.Model;
using FluentValidation;
using FluentValidation.Results;

public class ForecastUpdateCommand : CommandRequestBase
{
    public ForecastUpdateCommand(City city)
    {
        this.City = city;
    }

    public City City { get; }

    public override ValidationResult Validate() =>
        new Validator().Validate(this);

    public class Validator : AbstractValidator<ForecastUpdateCommand>
    {
        public Validator()
        {
            this.RuleFor(c => c.City).NotNull();
        }
    }
}
