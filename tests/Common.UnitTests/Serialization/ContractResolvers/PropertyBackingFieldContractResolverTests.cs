// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Serialization;

using Newtonsoft.Json;

// TODO: get rid of Newtonsoft dependency

[UnitTest("Common")]
public class PropertyBackingFieldContractResolverTests
{
    private readonly JsonSerializerSettings serializerSettings;

    public PropertyBackingFieldContractResolverTests()
    {
        this.serializerSettings = new JsonSerializerSettings { ContractResolver = new PropertyBackingFieldContractResolver() };
    }

    [Fact]
    public void CreateProperty_ObjectWithBackedCollection_DeserializesTheCollection()
    {
        // Arrange
        var faker = new Faker();
        var ticks = DateTime.UtcNow.Ticks;
        var entity = new PersonStub($"John {ticks}", $"Doe {ticks}", $"John.Doe{ticks}@gmail.com", 24);
        entity.AddLocation(LocationStub.Create(faker.Company.CompanyName(),
            faker.Address.StreetAddress(),
            faker.Address.BuildingNumber(),
            faker.Address.ZipCode(),
            faker.Address.City(),
            faker.Address.Country()));
        entity.AddLocation(LocationStub.Create(faker.Company.CompanyName(),
            faker.Address.StreetAddress(),
            faker.Address.BuildingNumber(),
            faker.Address.ZipCode(),
            faker.Address.City(),
            faker.Address.Country()));
        entity.AddLocation(LocationStub.Create(faker.Company.CompanyName(),
            faker.Address.StreetAddress(),
            faker.Address.BuildingNumber(),
            faker.Address.ZipCode(),
            faker.Address.City(),
            faker.Address.Country()));

        // Act
        var json = JsonConvert.SerializeObject(entity, this.serializerSettings);
        var result = JsonConvert.DeserializeObject<PersonStub>(json, this.serializerSettings);

        // Assert
        entity.ShouldNotBeNull();
        entity.Locations.Count()
            .ShouldBe(result.Locations.Count()); // make sure the locations collection (with private backing field)
        entity.Locations.Count()
            .ShouldBe(3);
        entity.Locations.First()
            .Name.ShouldBe(result.Locations.First()
                .Name);
        entity.Locations.Last()
            .Name.ShouldBe(result.Locations.Last()
                .Name);
    }
}