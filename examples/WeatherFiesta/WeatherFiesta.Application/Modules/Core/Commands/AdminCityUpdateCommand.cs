// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Domain;

/// <summary>
/// Admin command to update city details.
/// </summary>
[Command]
[HandlerTimeout(10000)]
public partial class AdminCityUpdateCommand
{
    public AdminCityUpdateCommand()
    {
    }

    public AdminCityUpdateCommand(string cityId, AdminCityUpdateModel model)
    {
        this.CityId = cityId;
        this.Model = model;
    }

    /// <summary>Gets the city identifier to update.</summary>
    [ValidateNotEmpty("City ID is required.")]
    [ValidateValidGuid("Invalid city ID format.")]
    public string CityId { get; private set; }

    [ValidateNotNull]
    public AdminCityUpdateModel Model { get; set; }

    [Validate]
    private static void Validate(InlineValidator<AdminCityUpdateCommand> validator)
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
        var cityId = Domain.Model.CityId.Create(this.CityId);

        var spec = new Specification<City>(c => c.Id == cityId);
        var cityResult = await City.FindAllAsync(spec, null, cancellationToken);
        if (cityResult.IsFailure)
        {
            return Result<CityModel>.Failure(cityResult.Errors.Select(e => e.Message));
        }

        var city = cityResult.Value.FirstOrDefault();
        if (city is null)
        {
            return Result<CityModel>.Failure("City not found.");
        }

        city.Name = this.Model.Name;
        city.Country = this.Model.Country;
        city.CountryCode = this.Model.CountryCode;
        city.TimeZone = this.Model.TimeZone;
        city.Location = Location.Create(this.Model.Latitude, this.Model.Longitude);
        city.Elevation = this.Model.Elevation;

        var result = await city.UpdateAsync(cancellationToken);
        if (result.IsFailure)
        {
            return Result<CityModel>.Failure(result.Errors.Select(e => e.Message));
        }

        return Result<CityModel>.Success(mapper.Map<City, CityModel>(result.Value));
    }
}
