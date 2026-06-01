// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Domain;

/// <summary>
/// Command to trigger weather data ingestion for a subscribed city.
/// Validates the subscription and returns accepted status.
/// Actual ingestion is handled by the scheduled job or admin trigger.
/// </summary>
[Command]
[HandlerTimeout(30000)]
public partial class CityIngestCommand
{
    public CityIngestCommand()
    {
    }

    public CityIngestCommand(string cityId)
    {
        this.CityId = cityId;
    }

    /// <summary>Gets the city identifier to trigger ingestion for.</summary>
    [ValidateNotEmpty("City ID is required.")]
    [ValidateValidGuid("Invalid city ID format.")]
    public string CityId { get; private set; }

    [Handle]
    private async Task<Result<Unit>> HandleAsync(
        ICurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.UserId;
        var cityId = Domain.Model.CityId.Create(this.CityId);

        var spec = new UserCityByUserAndCitySpecification(userId, cityId);
        var subscriptionsResult = await UserCity.FindAllAsync(spec, null, cancellationToken);
        if (subscriptionsResult.IsFailure)
        {
            return Result<Unit>.Failure(subscriptionsResult.Errors.Select(e => e.Message));
        }

        var subscriptions = subscriptionsResult.Value;
        if (!subscriptions.Any())
        {
            return Result<Unit>.Failure("City subscription not found.");
        }

        // Ingestion is handled by the scheduled job or admin trigger
        // This command just validates the subscription and returns accepted
        return Result<Unit>.Success(Unit.Value);
    }
}
