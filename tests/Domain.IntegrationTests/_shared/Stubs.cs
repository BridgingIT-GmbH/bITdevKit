// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.IntegrationTests;

using System.Linq.Expressions;
using BridgingIT.DevKit.Domain;
using Mapster;
using MapsterMapper;
using Model;

public class StubPerson : Entity<int>
{
    public string Firstname { get; set; }

    public string Surname { get; set; }
}

public class StubEntity : AggregateRoot<string>
{
    public string Country { get; set; }

    public string FirstName { get; set; }

    public string LastName { get; set; }

    public int Age { get; set; }
}

public class VersionedStubEntity : AggregateRoot<string>, IConcurrency
{
    public string Country { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public int Age { get; set; }
    public Guid ConcurrencyVersion { get; set; }
}

public class StubDbEntity
{
    public string Nation { get; set; }

    public string Identifier { get; set; }

    public string FullName { get; set; }

    public int YearOfBirth { get; set; }
}

public class StubHasNameSpecification : Specification<StubEntity>
{
    public StubHasNameSpecification(string firstName, string lastName)
    {
        EnsureArg.IsNotNull(firstName);
        EnsureArg.IsNotNull(lastName);

        this.FirstName = firstName;
        this.LastName = lastName;
    }

    public string FirstName { get; }

    public string LastName { get; }

    public override Expression<Func<StubEntity, bool>> ToExpression()
    {
        return p => p.FirstName == this.FirstName && p.LastName == this.LastName;
    }
}

public class StubHasIdSpecification : Specification<StubEntity> // TODO: this should be mocked
{
    public StubHasIdSpecification(string id)
    {
        EnsureArg.IsNotNull(id);

        this.Id = id;
    }

    public string Id { get; }

    public override Expression<Func<StubEntity, bool>> ToExpression()
    {
        return p => p.Id == this.Id;
    }
}

public static class StubEntityMapperFactory
{
    public static IMapper Create()
    {
        var config = new TypeAdapterConfig();

        config.NewConfig<StubEntity, StubDbEntity>()
            .Map(dest => dest.Identifier, src => src.Id)
            .Map(dest => dest.Nation, src => src.Country)
            .Map(dest => dest.FullName, src => $"{src.FirstName} {src.LastName}")
            .Map(dest => dest.YearOfBirth, src => DateTime.UtcNow.Year - src.Age);

        config.NewConfig<StubDbEntity, StubEntity>()
            .Map(dest => dest.Id, src => src.Identifier)
            .Map(dest => dest.Country, src => src.Nation)
            .Map(dest => dest.FirstName, src => src.FullName.Split(' ', StringSplitOptions.None).FirstOrDefault())
            .Map(dest => dest.LastName, src => src.FullName.Split(' ', StringSplitOptions.None).LastOrDefault())
            .Map(dest => dest.Age, src => DateTime.UtcNow.Year - src.YearOfBirth);

        return new Mapper(config);
    }
}