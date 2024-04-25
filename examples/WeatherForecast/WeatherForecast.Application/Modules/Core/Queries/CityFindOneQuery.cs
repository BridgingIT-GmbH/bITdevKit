// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Application.Modules.Core;

using System;
using BridgingIT.DevKit.Application.Queries;
using BridgingIT.DevKit.Common;
using FluentValidation;
using FluentValidation.Results;

public class CityFindOneQuery : QueryRequestBase<CityQueryResponse>, ICacheQuery
{
    public CityFindOneQuery(string name)
    {
        this.Name = name;
    }

    public CityFindOneQuery(double? longitude, double? latitude)
    {
        this.Longitude = longitude;
        this.Latitude = latitude;
    }

    public CityFindOneQuery(string name = null, double? longitude = null, double? latitude = null)
    {
        this.Name = name;
        this.Longitude = longitude;
        this.Latitude = latitude;
    }

    public string Name { get; }

    public double? Longitude { get; }

    public double? Latitude { get; }

    CacheQueryOptions ICacheQuery.Options => new()
    {
        Key = $"application_{nameof(CityFindOneQuery)}_{this.Name}_{this.Longitude}_{this.Latitude}".TrimEnd('_'),
        SlidingExpiration = new TimeSpan(0, 0, 30)
    };

    public override ValidationResult Validate() =>
        new Validator().Validate(this);

    public class Validator : AbstractValidator<CityFindOneQuery>
    {
        public Validator()
        {
            this.RuleFor(c => c.Name).NotNull().When(c => !c.Longitude.HasValue && !c.Latitude.HasValue);
            this.RuleFor(c => c.Longitude).NotNull().When(c => c.Latitude.HasValue && c.Name.IsNullOrEmpty());
            this.RuleFor(c => c.Latitude).NotNull().When(c => c.Longitude.HasValue && c.Name.IsNullOrEmpty());
        }
    }
}
