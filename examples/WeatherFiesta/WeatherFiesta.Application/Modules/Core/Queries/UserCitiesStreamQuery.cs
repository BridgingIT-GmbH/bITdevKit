// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using System.Runtime.CompilerServices;
using BridgingIT.DevKit.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Prepares bounded stored-weather output for the authenticated user's subscriptions.</summary>
/// <example><code>var result = await requester.SendAsync(new UserCitiesStreamQuery(), cancellationToken: ct);</code></example>
[Query]
[HandlerTimeout(5000)]
public partial class UserCitiesStreamQuery
{
    [Handle]
    private Task<Result<IAsyncEnumerable<UserCityModel>>> HandleAsync(IServiceScopeFactory scopes, ICurrentUserAccessor currentUser, IOptions<CoreModuleConfiguration> configuration, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.UserId))
        {
            return Task.FromResult(Result<IAsyncEnumerable<UserCityModel>>.Failure().WithError(new UnauthorizedError()));
        }

        var userId = currentUser.UserId;
        var staleThreshold = TimeSpan.FromMinutes(configuration.Value.StaleThresholdMinutes);
        return Task.FromResult(Result<IAsyncEnumerable<UserCityModel>>.Success(StreamAsync(scopes, userId, staleThreshold)));
    }

    private static async IAsyncEnumerable<UserCityModel> StreamAsync(IServiceScopeFactory scopes, string userId, TimeSpan staleThreshold, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        const int BATCH_SIZE = 100;
        var offset = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<UserCityModel> page = [];
            int sourceCount;
            await using (var scope = scopes.CreateAsyncScope())
            {
                var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();
                var options = new FindOptions<UserCity>
                {
                    NoTracking = true, Skip = offset, Take = BATCH_SIZE,
                    Orders = [new(e => e.DisplayOrder), new(e => e.Id)]
                };
                var subscriptions = await UserCity.FindAllAsync(new UserCitiesByUserSpecification(userId), options, cancellationToken);
                if (subscriptions.IsFailure) { throw new NdjsonStreamException("stream_source_failed"); }

                var entities = subscriptions.Value.ToList();
                sourceCount = entities.Count;
                if (sourceCount == 0) { yield break; }

                var ids = entities.Select(e => e.CityId).Distinct().ToArray();
                // Each Active Entity operation creates and releases its own provider scope.
                var weatherResult = await CurrentWeather.FindAllAsync(new Specification<CurrentWeather>(e => ids.Contains(e.CityId)), new FindOptions<CurrentWeather> { NoTracking = true, Take = BATCH_SIZE }, cancellationToken);
                var cityResult = await City.FindAllAsync(new Specification<City>(e => ids.Contains(e.Id)), new FindOptions<City> { NoTracking = true, Take = BATCH_SIZE }, cancellationToken);
                if (weatherResult.IsFailure || cityResult.IsFailure) { throw new NdjsonStreamException("stream_source_failed"); }

                var weatherByCity = weatherResult.Value.GroupBy(e => e.CityId).ToDictionary(g => g.Key, g => g.First());
                var cities = cityResult.Value.ToDictionary(e => e.Id);
                foreach (var subscription in entities)
                {
                    var model = mapper.Map<UserCity, UserCityModel>(subscription);
                    if (cities.TryGetValue(subscription.CityId, out var city)) { model.City = mapper.Map<City, CityModel>(city); }

                    if (weatherByCity.TryGetValue(subscription.CityId, out var weather))
                    {
                        model.CurrentWeather = mapper.Map<CurrentWeather, CurrentWeatherModel>(weather);
                        model.LastUpdatedText = model.CurrentWeather.LastUpdatedText;
                        model.StaleDataWarning = weather.IsStale(staleThreshold);
                        if (model.StaleDataWarning)
                        {
                            model.StaleDataWarningMessage = $"Data may be outdated - last updated {model.LastUpdatedText}";
                            model.CurrentWeather.StaleDataWarning = true;
                            model.CurrentWeather.StaleDataWarningMessage = model.StaleDataWarningMessage;
                        }
                    }

                    page.Add(model);
                }
            }

            foreach (var model in page)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return model;
            }

            if (sourceCount < BATCH_SIZE) { yield break; }

            offset = checked(offset + sourceCount);
        }
    }
}
