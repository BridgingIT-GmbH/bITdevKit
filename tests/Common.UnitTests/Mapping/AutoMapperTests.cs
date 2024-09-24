// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Mapping;

using System.Linq.Expressions;
using global::AutoMapper;
using global::AutoMapper.Extensions.ExpressionMapping;

[UnitTest("Common")]
public class AutoMapperTests
{
    [Fact]
    public void CanMap_Test()
    {
        // Arrange
        var config = new MapperConfiguration(cfg => cfg.AddProfile<MapperProfile>());
        config.AssertConfigurationIsValid();
        var source = new PersonStub { Age = 25, FirstName = "John", LastName = "Doe" };
        var mapper = new AutoMapper<PersonStub, PersonDtoStub>(config.CreateMapper());

        // Act
        var target = mapper.Map(source);

        // Assert
        target.ShouldNotBeNull();
        target.Age.ShouldBe(25); // mapped by automapper as there is no mapping defined
        target.FullName.ShouldBe("John Doe");
    }

    [Fact]
    public void CanMap2_Test()
    {
        // Arrange
        var config = new MapperConfiguration(cfg => cfg.AddProfile<MapperProfile>());
        config.AssertConfigurationIsValid();
        var source = new PersonStub { Age = 25, FirstName = "John", LastName = "Doe" };
        var mapper = new AutoMapper(config.CreateMapper());

        // Act
        var target = mapper.Map<PersonStub, PersonDtoStub>(source);

        // Assert
        target.ShouldNotBeNull();
        target.Age.ShouldBe(25); // mapped by automapper as there is no mapping defined
        target.FullName.ShouldBe("John Doe");
    }

    [Fact]
    public void CanMapNull_Test()
    {
        // Arrange
        PersonStub source = null;
        var config = new MapperConfiguration(cfg => cfg
            .CreateMap<PersonStub, PersonDtoStub>()
            .IgnoreAllUnmapped());
        config.AssertConfigurationIsValid();
        var mapper = new AutoMapper<PersonStub, PersonDtoStub>(config.CreateMapper());

        // Act
        var target1 = mapper.Map(source);
        var target2 = mapper.Map(source, true);

        // Assert
        target1.ShouldBeNull();
        target2.ShouldNotBeNull();
        target2.Age.ShouldBe(0);
        target2.FullName.ShouldBe(null);
    }

    [Fact]
    public void CanMapExpression1_Test()
    {
        // Arrange
        var sourceDto1 = new PersonDtoStub { Age = 25, FullName = "John Doe" };
        var sourceDto2 = new PersonDtoStub { Age = 5, FullName = "Mary Jane" };
        var sourcesDto = new[] { sourceDto1, sourceDto2 }.AsQueryable();
        var config = new MapperConfiguration(cfg => cfg
            //.AddExpressionMapping()
            .AddProfile<MapperProfile>());
        config.AssertConfigurationIsValid();
        var mapper = new AutoMapper<PersonStub, PersonDtoStub>(config.CreateMapper());
        var autoMapper = new Mapper(config);
        Expression<Func<PersonStub, bool>> expression = p => p.Age > 10;

        // Act
        var expressionDto = autoMapper.MapExpression<Expression<Func<PersonDtoStub, bool>>>(expression);
        var sources = sourcesDto.Where(expressionDto)
            .ToList();

        // Assert
        sources.ShouldNotBeNull();
        sources.Count.ShouldBe(1);
        sources[0]
            .Age.ShouldBe(25);
        sources[0]
            .FullName.ShouldBe("John Doe");
    }

    [Fact]
    public void CanMapExpression2_Test()
    {
        // Arrange
        var sourceDto1 = new PersonDtoStub { Age = 25, FullName = "John Doe" };
        var sourceDto2 = new PersonDtoStub { Age = 5, FullName = "Mary Jane" };
        var sourcesDto = new[] { sourceDto1, sourceDto2 }.AsQueryable();
        var config = new MapperConfiguration(cfg => cfg
            //.AddExpressionMapping()
            .AddProfile<MapperProfile>());
        config.AssertConfigurationIsValid();
        var mapper = new AutoMapper<PersonStub, PersonDtoStub>(config.CreateMapper());
        var autoMapper = new Mapper(config);
        Expression<Func<PersonStub, bool>> expression = p => p.FirstName == "John";

        // Act
        var expressionDto = autoMapper.MapExpression<Expression<Func<PersonDtoStub, bool>>>(expression);
        var sources = sourcesDto.Where(expressionDto)
            .ToList();

        // Assert
        sources.ShouldNotBeNull();
        sources.Count.ShouldBe(1);
        sources[0]
            .Age.ShouldBe(25);
        sources[0]
            .FullName.ShouldBe("John Doe");
    }

    private class MapperProfile : Profile
    {
        public MapperProfile()
        {
            this.CreateMap<PersonStub, PersonDtoStub>()
                .ForMember(d => d.FullName,
                    opt => opt.MapFrom(s => $"{s.FirstName} {s.LastName}".Trim()
                        .EmptyToNull()));
            //.IgnoreAllUnmapped();

            this.CreateMap<PersonDtoStub, PersonStub>()
                .ForMember(d => d.FirstName,
                    opt => opt.MapFrom(s => s.FullName.Split(' ', StringSplitOptions.None)
                        .FirstOrDefault()))
                .ForMember(d => d.LastName,
                    opt => opt.MapFrom(s => s.FullName.Split(' ', StringSplitOptions.None)
                        .LastOrDefault()))
                .ForMember(d => d.Nationality, opt => opt.Ignore())
                .ForMember(d => d.Email, opt => opt.Ignore())
                .ForMember(d => d.Locations, opt => opt.Ignore());
            //.IgnoreAllUnmapped();
        }
    }
}