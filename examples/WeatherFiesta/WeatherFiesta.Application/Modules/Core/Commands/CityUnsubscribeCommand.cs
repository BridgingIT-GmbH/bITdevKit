// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

/// <summary>
/// Command to unsubscribe the current user from a city.
/// Soft-deletes the subscription and closes DisplayOrder gaps.
/// </summary>
[Command]
[HandlerRetry(2, 300)]
[HandlerTimeout(5000)]
public partial class CityUnsubscribeCommand
{
    public CityUnsubscribeCommand()
    {
    }

    public CityUnsubscribeCommand(string cityId)
    {
        this.CityId = cityId;
    }

    /// <summary>Gets the city identifier to unsubscribe from.</summary>
    [ValidateNotEmpty("City ID is required.")]
    [ValidateValidGuid("Invalid city ID format.")]
    public string CityId { get; private set; }

    [Handle]
    private async Task<Result<Unit>> HandleAsync(
        ICurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.UserId;
        var cityId = Domain.Modules.Core.Model.CityId.Create(this.CityId);

        var spec = new UserCityByUserAndCitySpecification(userId, cityId);
        var userCitiesResult = await UserCity.FindAllAsync(spec, null, cancellationToken);
        if (userCitiesResult.IsFailure)
        {
            return userCitiesResult.Wrap<Unit>();
        }

        var userCities = userCitiesResult.Value;
        if (!userCities.Any())
        {
            return Result<Unit>.Failure("City subscription not found.");
        }

        var userCity = userCities.First();
        var wasPrimary = userCity.IsPrimary;

        // Soft-delete the subscription (also clears IsPrimary)
        userCity.SoftDelete();

        var result = await userCity.UpdateAsync(cancellationToken);
        if (result.IsFailure)
        {
            return result.Wrap<Unit>();
        }

        // Close DisplayOrder gaps for remaining subscriptions
        var allUserCitiesResult = await UserCity.FindAllAsync(
            new UserCitiesByUserSpecification(userId), null, cancellationToken);
        if (allUserCitiesResult.IsFailure)
        {
            return allUserCitiesResult.Wrap<Unit>();
        }

        var allUserCities = allUserCitiesResult.Value;
        var orderedCities = allUserCities
            .Where(uc => !uc.AuditState.IsDeleted() && uc.Id != userCity.Id)
            .OrderBy(uc => uc.DisplayOrder)
            .ToList();

        for (var i = 0; i < orderedCities.Count; i++)
        {
            if (orderedCities[i].DisplayOrder != i)
            {
                orderedCities[i].SetDisplayOrder(i);
                await orderedCities[i].UpdateAsync(cancellationToken);
            }
        }

        // If the deleted subscription was primary, promote the first remaining active subscription
        if (wasPrimary && orderedCities.Count > 0)
        {
            orderedCities[0].SetPrimary(true);
            await orderedCities[0].UpdateAsync(cancellationToken);
        }

        return Result<Unit>.Success(Unit.Value);
    }
}
