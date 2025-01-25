// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Domain.Model;

using System.Diagnostics;
using Common;
using DevKit.Domain.Model;

[DebuggerDisplay("Id={Id}, Name={Name} [{Country}]")]
public class City : AggregateRoot<Guid>
{
    private City() { }

    public string Name { get; private set; }

    public string Country { get; private set; }

    public DateTime? CreatedDate { get; private set; }

    public DateTime? UpdatedDate { get; private set; }

    public GeoLocation Location { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTime? DeletedDate { get; private set; }

    public string DeletedReason { get; private set; }

    public static City Create(string name, string country, double longitude, double latitude)
    {
        Rule.Check(new CountryShouldBeKnown(country));

        var entity = new City
        {
            Id = GuidGenerator.Create($"{country}-{name}"), // create repeatable id auf basis country-name (=upsert friendly)
            Name = name,
            Country = country,
            Location = GeoLocation.Create(longitude, latitude),
            CreatedDate = DateTime.UtcNow
        };
        entity.DomainEvents.Register(new CityCreatedDomainEvent(entity.Id, name));

        return entity;
    }

    public void Update(string name, string country, double longitude, double latitude)
    {
        Rule.Check(new CountryShouldBeKnown(country));

        this.Name = name;
        this.Country = country;
        this.Location = GeoLocation.Create(longitude, latitude);
        this.UpdatedDate = DateTime.UtcNow;
    }

    public void Delete(string reason)
    {
        Rule.Add(
                new DeleteMustBeProvidedReasonRule(reason),
                new DeleteCannotBeDoneTwiceRule(this.IsDeleted))
            .Check();

        this.IsDeleted = true;
        this.DeletedDate = DateTime.UtcNow;
        this.DeletedReason = reason;

        this.DomainEvents.Register(new CityDeletedDomainEvent(this.Id, reason));
    }
}