// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.Abstractions;

/// <summary>
/// Command to create a city and subscribe the current user to it.
/// Geocodes the city name via the geocoding client, deduplicates by ExternalId,
/// and creates or reactivates the user's subscription.
/// </summary>
[Command]
[HandlerRetry(2, 300)]
[HandlerTimeout(10000)]
public partial class CityCreateCommand
{
    [ValidateNotNull]
    public CityCreateModel Model { get; set; }

    [Validate]
    private static void Validate(InlineValidator<CityCreateCommand> validator)
    {
        validator.RuleFor(c => c.Model.Name).NotNull().NotEmpty().MinimumLength(3).MaximumLength(200);
        validator.RuleFor(c => c.Model.CountryCode).NotNull().NotEmpty().Length(2);
    }

    [Handle]
    private async Task<Result<CityModel>> HandleAsync(
        IMapper mapper,
        IWeatherGeocodingClient geocodingClient,
        ICurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.UserId;

        // Step 1: Geocode the city
        var geocodingResult = await geocodingClient.SearchCityAsync(
            this.Model.Name,
            this.Model.CountryCode,
            cancellationToken);

        if (geocodingResult is null)
        {
            return Result<CityModel>.Failure("No geocoding results found for the specified city name.");
        }

        // Step 2: Check if city already exists (dedup by ExternalId)
        var existingCitySpec = new CityByExternalIdSpecification(geocodingResult.ExternalId!.Value);
        var existingCitiesResult = await City.FindAllAsync(existingCitySpec, null, cancellationToken);
        if (existingCitiesResult.IsFailure)
        {
            return existingCitiesResult.Wrap<CityModel>();
        }

        var existingCities = existingCitiesResult.Value;
        City city;

        if (existingCities.Any())
        {
            city = existingCities.First();
        }
        else
        {
            // Step 3: Create and persist the city
            city = City.Create(
                geocodingResult.Name,
                geocodingResult.Country,
                geocodingResult.CountryCode,
                geocodingResult.TimeZone,
                Location.Create(geocodingResult.Latitude, geocodingResult.Longitude),
                geocodingResult.ExternalId,
                geocodingResult.Elevation);

            city.RegisterDomainEvent(new CityCreatedDomainEvent(city));

            var cityResult = await city.InsertAsync(cancellationToken);
            if (cityResult.IsFailure)
            {
                return cityResult.Wrap<CityModel>();
            }

            city = cityResult.Value;
        }

        // Step 4: Check if user already has a subscription (including soft-deleted)
        var userCitySpec = new UserCityByUserAndCityIncludingDeletedSpecification(userId, city.Id);
        var existingUserCitiesResult = await UserCity.FindAllAsync(userCitySpec, null, cancellationToken);
        if (existingUserCitiesResult.IsFailure)
        {
            return existingUserCitiesResult.Wrap<CityModel>();
        }

        var existingUserCities = existingUserCitiesResult.Value;
        UserCity userCity;
        if (existingUserCities.Any())
        {
            // Reactivate soft-deleted subscription
            userCity = existingUserCities.First();
            if (userCity.AuditState.IsDeleted())
            {
                userCity.Reactivate();
            }
        }
        else
        {
            // Step 5: Create new subscription (first city becomes primary)
            var existingCountResult = await UserCity.CountAsync(
                new UserCitiesByUserSpecification(userId), null, cancellationToken);
            if (existingCountResult.IsFailure)
            {
                return existingCountResult.Wrap<CityModel>();
            }

            var isPrimary = existingCountResult.Value == 0;

            userCity = UserCity.Create(userId, city.Id, isPrimary);
        }

        var userCityResult = await userCity.UpsertAsync(cancellationToken);
        if (userCityResult.IsFailure)
        {
            return userCityResult.Wrap<CityModel>();
        }

        return userCityResult.Wrap(mapper.Map<City, CityModel>(city));
    }
}

/// <summary>
/// Input model for creating a city subscription.
/// </summary>
public class CityCreateModel
{
    /// <summary>Gets or sets the city name to search for.</summary>
    public string Name { get; set; }

    /// <summary>Gets or sets the ISO country code to filter the search.</summary>
    public string CountryCode { get; set; }
}
