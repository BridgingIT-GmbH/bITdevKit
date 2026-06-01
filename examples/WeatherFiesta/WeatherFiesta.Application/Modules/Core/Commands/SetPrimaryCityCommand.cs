// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Domain;

/// <summary>
/// Command to set a city as the user's primary city.
/// Clears the primary flag on all other subscriptions first.
/// </summary>
[Command]
[HandlerTimeout(5000)]
public partial class SetPrimaryCityCommand
{
    public SetPrimaryCityCommand()
    {
    }

    public SetPrimaryCityCommand(string cityId)
    {
        this.CityId = cityId;
    }

    /// <summary>Gets the city identifier to set as primary.</summary>
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

        // Verify the user is subscribed to this city
        var targetSpec = new UserCityByUserAndCitySpecification(userId, cityId);
        var targetSubscriptionsResult = await UserCity.FindAllAsync(targetSpec, null, cancellationToken);
        if (targetSubscriptionsResult.IsFailure)
        {
            return Result<Unit>.Failure(targetSubscriptionsResult.Errors.Select(e => e.Message));
        }

        var targetSubscriptions = targetSubscriptionsResult.Value;
        if (!targetSubscriptions.Any())
        {
            return Result<Unit>.Failure("City subscription not found.");
        }

        // Clear IsPrimary on all user's subscriptions
        var allSpec = new UserCitiesByUserSpecification(userId);
        var allUserCitiesResult = await UserCity.FindAllAsync(allSpec, null, cancellationToken);
        if (allUserCitiesResult.IsFailure)
        {
            return Result<Unit>.Failure(allUserCitiesResult.Errors.Select(e => e.Message));
        }

        var allUserCities = allUserCitiesResult.Value;
        foreach (var uc in allUserCities)
        {
            uc.SetPrimary(false);
            await uc.UpdateAsync(cancellationToken);
        }

        // Set IsPrimary on the target subscription
        var target = targetSubscriptions.First();
        target.SetPrimary(true);
        await target.UpdateAsync(cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
