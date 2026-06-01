// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Domain;

/// <summary>
/// Admin command to create a city directly without geocoding.
/// </summary>
[Command]
[HandlerTimeout(10000)]
public partial class AdminCityCreateCommand
{
    [ValidateNotNull]
    public AdminCityCreateModel Model { get; set; }

    [Validate]
    private static void Validate(InlineValidator<AdminCityCreateCommand> validator)
    {
        validator.RuleFor(c => c.Model.Name).NotNull().NotEmpty().MinimumLength(2).MaximumLength(200);
        validator.RuleFor(c => c.Model.Country).NotNull().NotEmpty().MaximumLength(200);
        validator.RuleFor(c => c.Model.CountryCode).NotNull().NotEmpty().Length(2);
        validator.RuleFor(c => c.Model.TimeZone).NotNull().NotEmpty().MaximumLength(100);
    }

    [Handle]
    private async Task<Result<CityModel>> HandleAsync(
        IMapper mapper,
        CancellationToken cancellationToken)
    {
        var city = City.Create(
            this.Model.Name,
            this.Model.Country,
            this.Model.CountryCode,
            this.Model.TimeZone,
            Location.Create(this.Model.Latitude, this.Model.Longitude),
            this.Model.ExternalId,
            this.Model.Elevation);

        var result = await city.InsertAsync(cancellationToken);
        if (result.IsFailure)
        {
            return Result<CityModel>.Failure(result.Errors.Select(e => e.Message));
        }

        return Result<CityModel>.Success(mapper.Map<City, CityModel>(result.Value));
    }
}
