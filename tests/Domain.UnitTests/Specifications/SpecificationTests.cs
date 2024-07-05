// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Specifications;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Specifications;
using System.Linq.Expressions;
using AutoMapper;
using AutoMapper.Extensions.ExpressionMapping;

[UnitTest("Domain")]
public class SpecificationTests
{
    [Fact]
    public void ExpressionCtorIsSatisfiedBy_Test()
    {
        new Specification<PersonStub>(e => e.FirstName == "John")
            .IsSatisfiedBy(new PersonStub { FirstName = "John" })
            .ShouldBe(true);

        new Specification<PersonStub>(e => e.Age == int.MaxValue)
            .IsSatisfiedBy(new PersonStub { FirstName = "John", Age = int.MaxValue })
            .ShouldBe(true);
    }

    [Fact]
    public void IsNotSatisfiedBy_Test()
    {
        new Specification<PersonStub>(e => e.FirstName == "John")
            .IsSatisfiedBy(new PersonStub { FirstName = "Johny" })
            .ShouldBe(false);
    }

    [Fact]
    public void MapSpecification_Test()
    {
        // Arrange
        var sourceDto1 = new PersonDtoStub { Identifier = Guid.NewGuid(), Age = 25, FullName = "John Doe" };
        var sourceDto2 = new PersonDtoStub { Identifier = Guid.NewGuid(), Age = 5, FullName = "Mary Jane" };
        var sourcesDto = new PersonDtoStub[] { sourceDto1, sourceDto2 }.AsQueryable();
        var config = new MapperConfiguration(cfg => cfg
            //.AddExpressionMapping()
            .AddProfile<MapperProfile>());
        config.AssertConfigurationIsValid();
        var autoMapper = new Mapper(config);
        var specification = new Specification<PersonStub>(p => p.FirstName == "John");
        var specificationId = new Specification<PersonStub>(p => p.Id == sourceDto1.Identifier);

        // Act
        var expressionDto = autoMapper.MapExpression<Expression<Func<PersonDtoStub, bool>>>(specification.ToExpression());
        var expressionIdDto = autoMapper.MapExpression<Expression<Func<PersonDtoStub, bool>>>(specificationId.ToExpression());
        var sources = sourcesDto.Where(expressionDto).ToList();
        var sources2 = sourcesDto.Where(expressionIdDto).ToList();

        // Assert
        sources.ShouldNotBeNull();
        sources.Count.ShouldBe(1);
        sources[0].Age.ShouldBe(25);
        sources[0].FullName.ShouldBe("John Doe");

        sources2.ShouldNotBeNull();
        sources2.Count.ShouldBe(1);
        sources2[0].Age.ShouldBe(25);
        sources2[0].FullName.ShouldBe("John Doe");
    }

    private class MapperProfile : Profile
    {
        public MapperProfile()
        {
            this.CreateMap<PersonStub, PersonDtoStub>()
                .ForMember(d => d.Identifier, o => o.MapFrom(s => s.Id))
                .ForMember(d => d.Age, o => o.MapFrom(s => s.Age))
                .ForMember(d => d.FullName, opt => opt.MapFrom(s => $"{s.FirstName} {s.LastName}"))
                .IgnoreAllUnmapped();

            this.CreateMap<PersonDtoStub, PersonStub>()
                .ForMember(d => d.Id, o => o.MapFrom(s => s.Identifier))
                .ForMember(d => d.Age, o => o.MapFrom(s => s.Age))
                .ForMember(d => d.FirstName, opt => opt.MapFrom(s => s.FullName.Split(' ', StringSplitOptions.None).FirstOrDefault()))
                .ForMember(d => d.LastName, opt => opt.MapFrom(s => s.FullName.Split(' ', StringSplitOptions.None).LastOrDefault()))
                .IgnoreAllUnmapped();
        }
    }
}