// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

public class DinnerLocation : ValueObject
{
    private DinnerLocation() { }

    private DinnerLocation(
        string name,
        string addressLine1,
        string addressLine2,
        string postalCode,
        string city,
        string country,
        string websiteUrl = null,
        double? latitude = null,
        double? longitude = null)
    {
        this.Name = name;
        this.AddressLine1 = addressLine1;
        this.AddressLine2 = addressLine2;
        this.PostalCode = postalCode;
        this.City = city;
        this.Country = country;
        this.WebsiteUrl = websiteUrl;
        this.Latitude = latitude;
        this.Longitude = longitude;
    }

    public string Name { get; }

    public string AddressLine1 { get; }

    public string AddressLine2 { get; }

    public string PostalCode { get; private set; }

    public string City { get; }

    public string Country { get; }

    public string WebsiteUrl { get; private set; }

    public double? Latitude { get; private set; }

    public double? Longitude { get; private set; }

    public static DinnerLocation Create(
        string name,
        string addressLine1,
        string addressLine2,
        string postalCode,
        string city,
        string country,
        string websiteUrl = null,
        double? latitude = null,
        double? longitude = null)
    {
        Rule.Add(
            DinnerRules.CountryShouldBeKnown(country),
            DinnerRules.LongitudeShouldBeInRange(longitude),
            DinnerRules.LatitudeShouldBeInRange(latitude)
        ).Check();

        return new DinnerLocation(name,
            addressLine1,
            addressLine2,
            postalCode,
            city,
            country,
            websiteUrl,
            latitude,
            longitude);
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Name;
        yield return this.AddressLine1;
        yield return this.AddressLine2;
        yield return this.City;
        yield return this.Country;
    }
}