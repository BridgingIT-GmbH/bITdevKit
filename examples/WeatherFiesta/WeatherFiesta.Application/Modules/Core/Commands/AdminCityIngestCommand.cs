// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Domain;

/// <summary>
/// Admin command to trigger weather data ingestion for a city without subscription check.
/// </summary>
[Command]
[HandlerTimeout(30000)]
public partial class AdminCityIngestCommand
{
    public AdminCityIngestCommand()
    {
    }

    public AdminCityIngestCommand(string cityId)
    {
        this.CityId = cityId;
    }

    /// <summary>Gets the city identifier to trigger ingestion for.</summary>
    [ValidateNotEmpty("City ID is required.")]
    [ValidateValidGuid("Invalid city ID format.")]
    public string CityId { get; private set; }

    [Handle]
    private async Task<Result<Unit>> HandleAsync(
        CancellationToken cancellationToken)
    {
        var cityId = Domain.Model.CityId.Create(this.CityId);

        var spec = new Specification<City>(c => c.Id == cityId);
        var cityResult = await City.FindAllAsync(spec, null, cancellationToken);
        if (cityResult.IsFailure)
        {
            return Result<Unit>.Failure(cityResult.Errors.Select(e => e.Message));
        }

        var city = cityResult.Value.FirstOrDefault();
        if (city is null)
        {
            return Result<Unit>.Failure("City not found.");
        }

        // Ingestion is handled by the scheduled job or a dedicated ingestion service
        // This command validates the city exists and returns accepted
        return Result<Unit>.Success(Unit.Value);
    }
}
