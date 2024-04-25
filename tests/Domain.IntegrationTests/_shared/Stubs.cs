// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.IntegrationTests;

using System;
using System.Linq;
using System.Linq.Expressions;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Specifications;
using EnsureThat;

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

public static class StubEntityMapperConfiguration
{
    public static AutoMapper.IMapper Create()
    {
        var mapper = new AutoMapper.MapperConfiguration(c =>
        {
            c.CreateMap<StubEntity, StubDbEntity>()
                .ForMember(d => d.Identifier, o => o.MapFrom(s => s.Id))
                .ForMember(d => d.Nation, o => o.MapFrom(s => s.Country))
                //.ForMember(d => d.FullName, o => o.ResolveUsing(new FullNameResolver()))
                .ForMember(d => d.FullName, o => o.MapFrom(s => $"{s.FirstName} {s.LastName}"))
                .ForMember(d => d.YearOfBirth, o => o.MapFrom(new YearOfBirthResolver()));

            c.CreateMap<StubDbEntity, StubEntity>()
                .ForMember(d => d.Id, o => o.MapFrom(s => s.Identifier))
                .ForMember(d => d.Country, o => o.MapFrom(s => s.Nation))
                //.ForMember(d => d.FirstName, o => o.ResolveUsing(new FirstNameResolver()))
                .ForMember(d => d.FirstName,
                    o => o.MapFrom(s => s.FullName.Split(' ', StringSplitOptions.None).FirstOrDefault()))
                //.ForMember(d => d.LastName, o => o.ResolveUsing(new LastNameResolver()))
                .ForMember(d => d.LastName,
                    o => o.MapFrom(s => s.FullName.Split(' ', StringSplitOptions.None).LastOrDefault()))
                .ForMember(d => d.Age, o => o.MapFrom(new AgeResolver()));
        });

        mapper.AssertConfigurationIsValid();
        return mapper.CreateMapper();
    }

    //private class FullNameResolver : IValueResolver<StubEntity, StubDto, string>
    //{
    //    public string Resolve(StubEntity source, StubDto destination, string destMember, ResolutionContext context)
    //    {
    //        return $"{source.FirstName} {source.LastName}";
    //    }
    //}

    private class YearOfBirthResolver : AutoMapper.IValueResolver<StubEntity, StubDbEntity, int>
    {
        public int Resolve(StubEntity source, StubDbEntity destination, int destMember,
            AutoMapper.ResolutionContext context)
        {
            return DateTime.UtcNow.Year - source.Age;
        }
    }

    //private class FirstNameResolver : IValueResolver<StubDto, StubEntity, string>
    //{
    //    public string Resolve(StubDto source, StubEntity destination, string destMember, ResolutionContext context)
    //    {
    //        return source.FullName.NullToEmpty().Split(' ').FirstOrDefault();
    //    }
    //}

    //private class LastNameResolver : IValueResolver<StubDto, StubEntity, string>
    //{
    //    public string Resolve(StubDto source, StubEntity destination, string destMember, ResolutionContext context)
    //    {
    //        return source.FullName.NullToEmpty().Split(' ').LastOrDefault();
    //    }
    //}

    private class AgeResolver : AutoMapper.IValueResolver<StubDbEntity, StubEntity, int>
    {
        public int Resolve(StubDbEntity source, StubEntity destination, int destMember,
            AutoMapper.ResolutionContext context)
        {
            return DateTime.UtcNow.Year - source.YearOfBirth;
        }
    }
}