// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Specifications;

using System.Linq.Expressions;
using AutoMapper;
using AutoMapper.Extensions.ExpressionMapping;
using DevKit.Domain.Specifications;
using IMapper = AutoMapper.IMapper;

[UnitTest("Domain")]
public class MappedSpecificationTests
{
    private readonly IMapper mapper;

    public MappedSpecificationTests()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile<MapperProfile>());
        //config.AssertConfigurationIsValid();
        this.mapper = new Mapper(config);
    }

    [Fact]
    public void MapSpecification_SimplePropertyMapping_ShouldWorkCorrectly()
    {
        // Arrange
        var sut = new Specification<PersonStub>(p => p.Age > 18);
        var sourcesDto = new[]
        {
            new PersonDtoStub { Identifier = Guid.NewGuid(), Age = 25, FullName = "John Doe" }, new PersonDtoStub { Identifier = Guid.NewGuid(), Age = 15, FullName = "Jane Doe" }
        }.AsQueryable();

        // Act
        var mappedExpression = this.mapper.MapExpression<Expression<Func<PersonDtoStub, bool>>>(sut.ToExpression());
        var result = sourcesDto.Where(mappedExpression)
            .ToList();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0]
            .Age.ShouldBe(25);
        result[0]
            .FullName.ShouldBe("John Doe");
    }

    [Fact]
    public void MapSpecification_ComplexPropertyMapping_ShouldWorkCorrectly()
    {
        // Arrange
        var sut = new Specification<PersonStub>(p => p.FirstName == "John");
        var sourcesDto = new[]
        {
            new PersonDtoStub { Identifier = Guid.NewGuid(), Age = 25, FullName = "John Doe" }, new PersonDtoStub { Identifier = Guid.NewGuid(), Age = 30, FullName = "Jane Smith" }
        }.AsQueryable();

        // Act
        var mappedExpression = this.mapper.MapExpression<Expression<Func<PersonDtoStub, bool>>>(sut.ToExpression());
        var result = sourcesDto.Where(mappedExpression)
            .ToList();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0]
            .FullName.ShouldBe("John Doe");
    }

    [Fact]
    public void MapSpecification_IdMapping_ShouldWorkCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sut = new Specification<PersonStub>(p => p.Id == id);
        var sourcesDto = new[]
        {
            new PersonDtoStub { Identifier = id, Age = 25, FullName = "John Doe" }, new PersonDtoStub { Identifier = Guid.NewGuid(), Age = 30, FullName = "Jane Smith" }
        }.AsQueryable();

        // Act
        var mappedExpression = this.mapper.MapExpression<Expression<Func<PersonDtoStub, bool>>>(sut.ToExpression());
        var result = sourcesDto.Where(mappedExpression)
            .ToList();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0]
            .Identifier.ShouldBe(id);
        result[0]
            .FullName.ShouldBe("John Doe");
    }

    [Fact]
    public void MapSpecification_AndSpecification_ShouldWorkCorrectly()
    {
        // Arrange
        var sut = new Specification<PersonStub>(p => p.Age > 20 && p.FirstName == "John");
        var sourcesDto = new[]
        {
            new PersonDtoStub { Identifier = Guid.NewGuid(), Age = 25, FullName = "John Doe" }, new PersonDtoStub { Identifier = Guid.NewGuid(), Age = 30, FullName = "Jane Smith" },
            new PersonDtoStub { Identifier = Guid.NewGuid(), Age = 18, FullName = "John Smith" }
        }.AsQueryable();

        // Act
        var mappedExpression = this.mapper.MapExpression<Expression<Func<PersonDtoStub, bool>>>(sut.ToExpression());
        var result = sourcesDto.Where(mappedExpression)
            .ToList();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0]
            .FullName.ShouldBe("John Doe");
        result[0]
            .Age.ShouldBe(25);
    }

    [Fact]
    public void MapSpecification_OrSpecification_ShouldWorkCorrectly()
    {
        // Arrange
        var sut = new Specification<PersonStub>(p => p.Age > 25 || p.FirstName == "John");
        var sourcesDto = new[]
        {
            new PersonDtoStub { Identifier = Guid.NewGuid(), Age = 25, FullName = "John Doe" }, new PersonDtoStub { Identifier = Guid.NewGuid(), Age = 30, FullName = "Jane Smith" },
            new PersonDtoStub { Identifier = Guid.NewGuid(), Age = 20, FullName = "Bob Johnson" }
        }.AsQueryable();

        // Act
        var mappedExpression = this.mapper.MapExpression<Expression<Func<PersonDtoStub, bool>>>(sut.ToExpression());
        var result = sourcesDto.Where(mappedExpression)
            .ToList();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result.ShouldContain(p => p.FullName == "John Doe");
        result.ShouldContain(p => p.FullName == "Jane Smith");
    }

    [Fact]
    public void MapSpecification_NotSpecification_ShouldWorkCorrectly()
    {
        // Arrange
        var sut = new Specification<PersonStub>(p => p.Age <= 25).Not();
        var sourcesDto = new[]
        {
            new PersonDtoStub { Identifier = Guid.NewGuid(), Age = 25, FullName = "John Doe" }, new PersonDtoStub { Identifier = Guid.NewGuid(), Age = 30, FullName = "Jane Smith" },
            new PersonDtoStub { Identifier = Guid.NewGuid(), Age = 20, FullName = "Bob Johnson" }
        }.AsQueryable();

        // Act
        var mappedExpression = this.mapper.MapExpression<Expression<Func<PersonDtoStub, bool>>>(sut.ToExpression());
        var result = sourcesDto.Where(mappedExpression)
            .ToList();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0]
            .FullName.ShouldBe("Jane Smith");
        result[0]
            .Age.ShouldBe(30);
    }

    [Fact]
    public void MapSpecification_ComplexSpecification_ShouldWorkCorrectly()
    {
        // Arrange
        var sut = new Specification<PersonStub>(p => p.Age > 25 && p.FirstName == "John" || p.Age < 22 && p.LastName == "Johnson");
        var sourcesDto = new[]
        {
            new PersonDtoStub { Identifier = Guid.NewGuid(), Age = 30, FullName = "John Doe" }, new PersonDtoStub { Identifier = Guid.NewGuid(), Age = 20, FullName = "Bob Johnson" },
            new PersonDtoStub { Identifier = Guid.NewGuid(), Age = 25, FullName = "Jane Smith" },
            new PersonDtoStub { Identifier = Guid.NewGuid(), Age = 35, FullName = "Alice Brown" }
        }.AsQueryable();

        // Act
        var mappedExpression = this.mapper.MapExpression<Expression<Func<PersonDtoStub, bool>>>(sut.ToExpression());
        var result = sourcesDto.Where(mappedExpression)
            .ToList();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result.ShouldContain(p => p.FullName == "John Doe");
        result.ShouldContain(p => p.FullName == "Bob Johnson");
    }

    private class MapperProfile : Profile
    {
        public MapperProfile()
        {
            this.CreateMap<PersonStub, PersonDtoStub>()
                .ForMember(d => d.Identifier, o => o.MapFrom(s => s.Id))
                .ForMember(d => d.Age, o => o.MapFrom(s => s.Age))
                .ForMember(d => d.FullName, opt => opt.MapFrom(s => $"{s.FirstName} {s.LastName}"))
                .ReverseMap()
                .ForMember(d => d.Id, o => o.MapFrom(s => s.Identifier))
                .ForMember(d => d.FirstName,
                    opt => opt.MapFrom(s => s.FullName.Split(' ', StringSplitOptions.None)
                        .FirstOrDefault()))
                .ForMember(d => d.LastName,
                    opt => opt.MapFrom(s => s.FullName.Split(' ', StringSplitOptions.None)
                        .LastOrDefault()));
        }
    }
}