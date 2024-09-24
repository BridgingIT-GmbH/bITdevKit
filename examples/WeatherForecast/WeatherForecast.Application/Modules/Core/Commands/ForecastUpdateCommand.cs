// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Application.Modules.Core;

using DevKit.Application.Commands;
using Domain.Model;
using FluentValidation;
using FluentValidation.Results;

public class ForecastUpdateCommand(City city) : CommandRequestBase
{
    public City City { get; } = city;

    public override ValidationResult Validate()
    {
        return new Validator().Validate(this);
    }

    public class Validator : AbstractValidator<ForecastUpdateCommand>
    {
        public Validator()
        {
            this.RuleFor(c => c.City).NotNull();
        }
    }
}