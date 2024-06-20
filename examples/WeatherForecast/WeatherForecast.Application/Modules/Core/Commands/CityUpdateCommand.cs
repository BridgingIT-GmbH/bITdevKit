// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Application.Modules.Core;

using System;
using BridgingIT.DevKit.Application.Commands;
using FluentValidation;
using FluentValidation.Results;

public class CityUpdateCommand(CityModel model) : CommandRequestBase<AggregateUpdatedCommandResult>,
    ICacheInvalidateCommand, IRetryCommand
{
    public CityModel Model { get; } = model;

    CacheInvalidateCommandOptions ICacheInvalidateCommand.Options => new() { Key = "application_" };

    RetryCommandOptions IRetryCommand.Options => new() { Attempts = 3, Backoff = new TimeSpan(0, 0, 0, 1) };

    public override ValidationResult Validate() =>
        new Validator().Validate(this);

    public class Validator : AbstractValidator<CityUpdateCommand>
    {
        public Validator()
        {
            this.RuleFor(c => c.Model).NotNull();
            this.RuleFor(c => c.Model.Id).Must(id => id != Guid.Empty).WithMessage("Invalid guid.");
            this.RuleFor(c => c.Model.Name).NotNull();
            this.RuleFor(c => c.Model.Country).NotNull().NotEmpty();
            this.RuleFor(c => c.Model.Longitude).NotNull().NotEmpty();
            this.RuleFor(c => c.Model.Latitude).NotNull().NotEmpty();
        }
    }
}
