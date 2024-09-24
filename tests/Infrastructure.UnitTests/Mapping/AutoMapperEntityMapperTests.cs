// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.Mapping;

using System.Linq.Expressions;
using AutoMapper;
using Domain.Specifications;
using Infrastructure.Mapping;

[UnitTest("Infrastructure")]
public class AutoMapperEntityMapperTests
{
    [Fact]
    public void MapExpression_Test()
    {
        // Arrange
        var sourceDto1 = new PersonDtoStub { Identifier = Guid.NewGuid(), Age = 25, FullName = "John Doe" };
        var sourceDto2 = new PersonDtoStub { Identifier = Guid.NewGuid(), Age = 5, FullName = "Mary Jane" };
        var sourcesDto = new[] { sourceDto1, sourceDto2 }.AsQueryable();
        var config = new MapperConfiguration(cfg => cfg
            //.AddExpressionMapping()
            .AddProfile<MapperProfile>());
        config.AssertConfigurationIsValid();
        var entityMapper = new AutoMapperEntityMapper(config);
        Expression<Func<PersonStub, bool>> expression = p => p.FirstName == "John";
        Expression<Func<PersonStub, bool>> expressionId = p => p.Id.Equals(sourceDto1.Identifier);

        // Act
        var expressionDto = entityMapper.MapExpression<Expression<Func<PersonDtoStub, bool>>>(expression);
        var expressionIdDto = entityMapper.MapExpression<Expression<Func<PersonDtoStub, bool>>>(expressionId);
        var sources = sourcesDto.Where(expressionDto)
            .ToList();
        var sources2 = sourcesDto.Where(expressionIdDto)
            .ToList();

        // Assert
        sources.ShouldNotBeNull();
        sources.Count.ShouldBe(1);
        sources[0]
            .Age.ShouldBe(25);
        sources[0]
            .FullName.ShouldBe("John Doe");

        sources2.ShouldNotBeNull();
        sources2.Count.ShouldBe(1);
        sources2[0]
            .Age.ShouldBe(25);
        sources2[0]
            .FullName.ShouldBe("John Doe");
    }

    [Fact]
    public void MapSpecification_Test()
    {
        // Arrange
        var sourceDto1 = new PersonDtoStub { Identifier = Guid.NewGuid(), Age = 25, FullName = "John Doe" };
        var sourceDto2 = new PersonDtoStub { Identifier = Guid.NewGuid(), Age = 5, FullName = "Mary Jane" };
        var sourcesDto = new[] { sourceDto1, sourceDto2 }.AsQueryable();
        var config = new MapperConfiguration(cfg => cfg
            //.AddExpressionMapping()
            .AddProfile<MapperProfile>());
        config.AssertConfigurationIsValid();
        var entityMapper = new AutoMapperEntityMapper(config);
        var specification = new Specification<PersonStub>(p => p.FirstName == "John");
        var specificationId = new Specification<PersonStub>(p => p.Id.Equals(sourceDto1.Identifier));

        // Act
        var expressionDto = entityMapper.MapSpecification<PersonStub, PersonDtoStub>(specification);
        var expressionIdDto = entityMapper.MapSpecification<PersonStub, PersonDtoStub>(specificationId);
        var sources = sourcesDto.Where(expressionDto)
            .ToList();
        var sources2 = sourcesDto.Where(expressionIdDto)
            .ToList();

        // Assert
        sources.ShouldNotBeNull();
        sources.Count.ShouldBe(1);
        sources[0]
            .Age.ShouldBe(25);
        sources[0]
            .FullName.ShouldBe("John Doe");

        sources2.ShouldNotBeNull();
        sources2.Count.ShouldBe(1);
        sources2[0]
            .Age.ShouldBe(25);
        sources2[0]
            .FullName.ShouldBe("John Doe");
    }

    //private class MapperProfile : Profile
    //{
    //    public MapperProfile()
    //    {
    //        this.CreateMap<PersonStub, PersonDtoStub>()
    //            .ForMember(d => d.Identifier, o => o.MapFrom(s => s.Id))
    //            .ForMember(d => d.Age, o => o.MapFrom(s => s.Age))
    //            .ForMember(d => d.FullName, opt => opt.MapFrom(s => $"{s.FirstName} {s.LastName}"))
    //            .IgnoreAllUnmapped();

    //        this.CreateMap<PersonDtoStub, PersonStub>()
    //            .ForMember(d => d.Id, o => o.MapFrom(s => s.Identifier))
    //            .ForMember(d => d.Age, o => o.MapFrom(s => s.Age))
    //            .ForMember(d => d.FirstName, opt => opt.MapFrom(s => s.FullName.Split(' ', StringSplitOptions.None).FirstOrDefault()))
    //            .ForMember(d => d.LastName, opt => opt.MapFrom(s => s.FullName.Split(' ', StringSplitOptions.None).LastOrDefault()))
    //            .IgnoreAllUnmapped();
    //    }
    //}
}