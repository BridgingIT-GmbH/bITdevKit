// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Domain;

/// <summary>
/// Admin query to list all cities with subscription counts.
/// </summary>
[Query]
[HandlerTimeout(10000)]
public partial class AdminCitiesQuery
{
    [Handle]
    private async Task<Result<List<AdminCityModel>>> HandleAsync(
        IMapper mapper,
        CancellationToken cancellationToken)
    {
        var citiesResult = await City.FindAllAsync(null, cancellationToken);
        if (citiesResult.IsFailure)
        {
            return Result<List<AdminCityModel>>.Failure(citiesResult.Errors.Select(e => e.Message));
        }

        var cities = citiesResult.Value;
        var result = new List<AdminCityModel>();

        foreach (var city in cities)
        {
            var model = mapper.Map<City, AdminCityModel>(city);

            // Count active subscriptions for this city
            var subSpec = new Specification<UserCity>(uc => uc.CityId == city.Id && !uc.AuditState.IsDeleted());
            var subCountResult = await UserCity.CountAsync(subSpec, null, cancellationToken);
            if (subCountResult.IsFailure)
            {
                return Result<List<AdminCityModel>>.Failure(subCountResult.Errors.Select(e => e.Message));
            }

            model.SubscriptionCount = (int)subCountResult.Value;
            result.Add(model);
        }

        return Result<List<AdminCityModel>>.Success(result);
    }
}
