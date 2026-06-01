// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

/// <summary>
/// Command to reorder the user's city subscriptions.
/// Updates DisplayOrder based on the position in the provided list.
/// </summary>
[Command]
[HandlerTimeout(5000)]
public partial class ReorderCitiesCommand
{
    /// <summary>Gets or sets the ordered list of city identifiers.</summary>
    [ValidateNotNull]
    public List<string> CityIds { get; set; }

    [Validate]
    private static void Validate(InlineValidator<ReorderCitiesCommand> validator)
    {
        validator.RuleFor(c => c.CityIds).NotNull().Must(ids => ids.Count >= 2).WithMessage("At least 2 cities are required to reorder.");
        validator.RuleFor(c => c.CityIds).Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("Duplicate city IDs are not allowed.");
    }

    [Handle]
    private async Task<Result<Unit>> HandleAsync(
        ICurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.UserId;

        // Verify all city IDs are subscribed by the user
        var allSpec = new UserCitiesByUserSpecification(userId);
        var userCitiesResult = await UserCity.FindAllAsync(allSpec, null, cancellationToken);
        if (userCitiesResult.IsFailure)
        {
            return userCitiesResult.Wrap<Unit>();
        }

        var userCities = userCitiesResult.Value;
        var userCityIds = userCities.Select(uc => uc.CityId.Value.ToString()).ToHashSet();

        foreach (var cityId in this.CityIds)
        {
            if (!userCityIds.Contains(cityId))
            {
                return Result<Unit>.Failure($"City {cityId} is not subscribed by the current user.");
            }
        }

        // Reorder: set DisplayOrder based on position in the list
        for (var i = 0; i < this.CityIds.Count; i++)
        {
            var cityId = CityId.Create(this.CityIds[i]);
            var userCity = userCities.First(uc => uc.CityId == cityId);
            userCity.SetDisplayOrder(i);
            await userCity.UpdateAsync(cancellationToken);
        }

        return Result<Unit>.Success(Unit.Value);
    }
}
