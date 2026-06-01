// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core;

using System.Linq.Expressions;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Model;

/// <summary>
/// Finds a city by its ExternalId (Open-Meteo geocoding ID).
/// </summary>
public class CityByExternalIdSpecification(long externalId) : Specification<City>
{
    public override Expression<Func<City, bool>> ToExpression()
    {
        return c => c.ExternalId == externalId;
    }
}

/// <summary>
/// Finds active (non-deleted) UserCity subscriptions for a given user, ordered by DisplayOrder.
/// </summary>
public class UserCitiesByUserSpecification(string userId) : Specification<UserCity>
{
    public override Expression<Func<UserCity, bool>> ToExpression()
    {
        return uc => uc.UserId == userId && uc.AuditState.Deleted != true;
    }
}

/// <summary>
/// Finds a specific active UserCity subscription by UserId and CityId.
/// </summary>
public class UserCityByUserAndCitySpecification(string userId, CityId cityId) : Specification<UserCity>
{
    public override Expression<Func<UserCity, bool>> ToExpression()
    {
        return uc => uc.UserId == userId && uc.CityId == cityId && uc.AuditState.Deleted != true;
    }
}

/// <summary>
/// Finds a UserCity subscription (including soft-deleted) by UserId and CityId.
/// Used for reactivation checks.
/// </summary>
public class UserCityByUserAndCityIncludingDeletedSpecification(string userId, CityId cityId) : Specification<UserCity>
{
    public override Expression<Func<UserCity, bool>> ToExpression()
    {
        return uc => uc.UserId == userId && uc.CityId == cityId;
    }
}

/// <summary>
/// Finds cities whose weather data is stale (RetrievedAt older than threshold).
/// Used by the ingestion job to determine which cities need fresh data.
/// </summary>
public class StaleCitiesForIngestionSpecification(TimeSpan staleThreshold) : Specification<City>
{
    public override Expression<Func<City, bool>> ToExpression()
    {
        var cutoff = DateTime.UtcNow - staleThreshold;
        return c => c.CurrentWeather == null || c.CurrentWeather.RetrievedAt < cutoff;
    }
}
