// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Domain.Model;

using BridgingIT.DevKit.Common;
using DevKit.Domain.Model;

public class GeoLocation : ValueObject
{
    private GeoLocation() { }

    private GeoLocation(double longitude, double latitude)
    {
        this.Longitude = longitude;
        this.Latitude = latitude;
    }

    public double Longitude { get; private set; }

    public double Latitude { get; private set; }

    public static GeoLocation Create(double longitude, double latitude)
    {
        Rule.Add(
                new LongitudeShouldBeInRange(longitude),
                new LatitudeShouldBeInRange(latitude))
            .Check();

        return new GeoLocation { Longitude = longitude, Latitude = latitude };
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Longitude;
        yield return this.Latitude;
    }
}