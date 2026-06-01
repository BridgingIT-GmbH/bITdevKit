// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

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
            var locationResult = Location.Create(geocodingResult.Latitude, geocodingResult.Longitude);
            if (locationResult.IsFailure)
            {
                return locationResult.Wrap<CityModel>();
            }

            city = City.Create(
                geocodingResult.Name,
                geocodingResult.Country,
                geocodingResult.CountryCode,
                geocodingResult.TimeZone,
                locationResult.Value,
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
        var userCityExists = existingUserCities.Any();
        var userCity = userCityExists ? existingUserCities.First() : null;
        var needsCitySlot = userCity is null || userCity.AuditState.IsDeleted();

        long existingActiveCount = 0;
        if (needsCitySlot)
        {
            // Step 5: Enforce subscription plan city limit
            var subscriptionResult = await this.GetOrCreateSubscriptionAsync(userId, cancellationToken);
            if (subscriptionResult.IsFailure)
            {
                return subscriptionResult.Wrap<CityModel>();
            }

            var existingCountResult = await UserCity.CountAsync(
                new UserCitiesByUserSpecification(userId), null, cancellationToken);
            if (existingCountResult.IsFailure)
            {
                return existingCountResult.Wrap<CityModel>();
            }

            var cityLimitResult = Rule.Check(new SubscriptionCityLimitRule(subscriptionResult.Value, existingCountResult.Value));
            if (cityLimitResult.IsFailure)
            {
                return Result<CityModel>.Failure(cityLimitResult);
            }
        }

        if (userCityExists)
        {
            // Reactivate soft-deleted subscription
            if (userCity.AuditState.IsDeleted())
            {
                userCity.Reactivate();
            }
        }
        else
        {
            // Create new subscription (first city becomes primary)
            var isPrimary = existingActiveCount == 0;
            userCity = UserCity.Create(userId, city.Id, isPrimary);
        }

        var userCityResult = await userCity.UpsertAsync(cancellationToken);
        if (userCityResult.IsFailure)
        {
            return userCityResult.Wrap<CityModel>();
        }

        return userCityResult.Wrap(mapper.Map<City, CityModel>(city));
    }

    private async Task<Result<UserSubscription>> GetOrCreateSubscriptionAsync(string userId, CancellationToken cancellationToken)
    {
        var result = await UserSubscription.FindAllAsync(new SubscriptionByUserSpecification(userId), null, cancellationToken);
        if (result.IsFailure)
        {
            return Result<UserSubscription>.Failure(result);
        }

        var subscriptions = result.Value;
        if (subscriptions.Any())
        {
            var subscription = subscriptions
                .OrderBy(s => s.AuditState.IsDeleted())
                .ThenBy(s => s.StartDate)
                .First();

            if (subscription.AuditState.IsDeleted())
            {
                subscription.Reactivate();
                var updateResult = await subscription.UpsertAsync(cancellationToken);
                if (updateResult.IsFailure)
                {
                    return updateResult.Wrap<UserSubscription>();
                }

                subscription = updateResult.Value.entity;
            }

            if (!subscription.IsActive)
            {
                return Result<UserSubscription>.Failure(new DomainPolicyError(["Subscription is not active."]));
            }

            return Result<UserSubscription>.Success(subscription);
        }

        var newSubscription = UserSubscription.CreateFree(userId);
        var insertResult = await newSubscription.InsertAsync(cancellationToken);
        return insertResult;
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
