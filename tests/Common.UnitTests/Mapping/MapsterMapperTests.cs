﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Mapping;

using Mapster;

[UnitTest("Common")]
public class MapsterMapperTests
{
    [Fact]
    public void CanMap_Test()
    {
        // Arrange
        var config = MapperConfig.Create();
        var source = new PersonStub { Age = 25, FirstName = "John", LastName = "Doe" };
        var mapper = new MapsterMapper<PersonStub, PersonDtoStub>(config);

        // Act
        var target = mapper.Map(source);

        var d = new PersonDtoStub();
        var t2 = source.Adapt(d, config);

        // Assert
        target.ShouldNotBeNull();
        target.Age.ShouldBe(25);
        target.FullName.ShouldBe("John Doe");
    }

    [Fact]
    public void CanMap2_Test()
    {
        // Arrange
        var config = MapperConfig.Create();
        var source = new PersonStub { Age = 25, FirstName = "John", LastName = "Doe" };
        var mapper = new MapsterMapper(config);

        // Act
        var target = mapper.Map<PersonStub, PersonDtoStub>(source);

        // Assert
        target.ShouldNotBeNull();
        target.Age.ShouldBe(25);
        target.FullName.ShouldBe("John Doe");
    }

    [Fact]
    public void CanMapNull_Test()
    {
        // Arrange
        PersonStub source = null;
        var config = MapperConfig.Create();
        var mapper = new MapsterMapper<PersonStub, PersonDtoStub>(config);

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
    public void Map_WithExistingTarget_ShouldUpdateTargetProperties()
    {
        // Arrange
        var config = MapperConfig.Create();
        var source = new PersonStub { Age = 30, FirstName = "Jane", LastName = "Smith" };
        var target = new PersonDtoStub { Age = 25, FullName = "John Doe" };
        var sut = new MapsterMapper(config);

        // Act
        var result = sut.Map(source, target);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeSameAs(target);
        result.Age.ShouldBe(30);
        result.FullName.ShouldBe("Jane Smith");
    }

    [Fact]
    public void Map_WithNullSource_ShouldReturnUnchangedTarget()
    {
        // Arrange
        var config = MapperConfig.Create();
        PersonStub source = null;
        var target = new PersonDtoStub { Age = 25, FullName = "John Doe" };
        var sut = new MapsterMapper(config);

        // Act
        var result = sut.Map(source, target);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeSameAs(target);
        result.Age.ShouldBe(25);
        result.FullName.ShouldBe("John Doe");
    }

    [Fact]
    public void Map_WithNullTarget_ShouldReturnNewTargetInstance()
    {
        // Arrange
        var config = MapperConfig.Create();
        var source = new PersonStub { Age = 30, FirstName = "Jane", LastName = "Smith" };
        PersonDtoStub target = null;
        var sut = new MapsterMapper(config);

        // Act
        var result = sut.Map(source, target);

        // Assert
        result.ShouldNotBeNull();
    }

    [Fact]
    public void Map_WithRandomData_ShouldMapCorrectly()
    {
        // Arrange
        var config = MapperConfig.Create();
        var faker = new Faker();
        var source = new PersonStub { Age = faker.Random.Number(18, 80), FirstName = faker.Name.FirstName(), LastName = faker.Name.LastName() };
        var target = new PersonDtoStub { Age = faker.Random.Number(18, 80), FullName = faker.Name.FullName() };
        var sut = new MapsterMapper(config);

        // Act
        var result = sut.Map(source, target);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeSameAs(target);
        result.Age.ShouldBe(source.Age);
        result.FullName.ShouldBe($"{source.FirstName} {source.LastName}");
    }

    //[Fact]
    //public void CanMapExpression1_Test()
    //{
    //    // Arrange
    //    var sourceDto1 = new PersonDtoStub { Age = 25, FullName = "John Doe" };
    //    var sourceDto2 = new PersonDtoStub { Age = 5, FullName = "Mary Jane" };
    //    var sourcesDto = new PersonDtoStub[] { sourceDto1, sourceDto2 }.AsQueryable();
    //    var config = MapperConfig.Create();
    //    var mapper = new MapsterMapper<PersonStub, PersonDtoStub>(config);
    //    var autoMapper = new Mapper(config);
    //    Expression<Func<PersonStub, bool>> expression = p => p.Age > 10;

    //    // Act
    //    var expressionDto = autoMapper.MapExpression<Expression<Func<PersonDtoStub, bool>>>(expression);
    //    var sources = sourcesDto.Where(expressionDto).ToList();

    //    // Assert
    //    sources.ShouldNotBeNull();
    //    sources.Count.ShouldBe(1);
    //    sources[0].Age.ShouldBe(25);
    //    sources[0].FullName.ShouldBe("John Doe");
    //}

    //[Fact]
    //public void CanMapExpression2_Test()
    //{
    //    // Arrange
    //    var sourceDto1 = new PersonDtoStub { Age = 25, FullName = "John Doe" };
    //    var sourceDto2 = new PersonDtoStub { Age = 5, FullName = "Mary Jane" };
    //    var sourcesDto = new PersonDtoStub[] { sourceDto1, sourceDto2 }.AsQueryable();
    //    var config = MapperConfig.Create();
    //    var mapper = new MapsterMapper<PersonStub, PersonDtoStub>(config);
    //    var autoMapper = new Mapper(config);
    //    Expression<Func<PersonStub, bool>> expression = p => p.FirstName == "John";

    //    // Act
    //    var expressionDto = autoMapper.MapExpression<Expression<Func<PersonDtoStub, bool>>>(expression);
    //    var sources = sourcesDto.Where(expressionDto).ToList();

    //    // Assert
    //    sources.ShouldNotBeNull();
    //    sources.Count.ShouldBe(1);
    //    sources[0].Age.ShouldBe(25);
    //    sources[0].FullName.ShouldBe("John Doe");
    //}

    public static class MapperConfig
    {
        public static TypeAdapterConfig Create()
        {
            var config = new TypeAdapterConfig();

            config.ForType<PersonStub, PersonDtoStub>()
                .Map(dest => dest.FullName,
                    src => $"{src.FirstName} {src.LastName}".Trim()
                        .EmptyToNull());

            config.ForType<PersonDtoStub, PersonStub>()
                .Map(dest => dest.FirstName,
                    src => src.FullName.Split(' ', StringSplitOptions.None)
                        .FirstOrDefault())
                .Map(dest => dest.LastName,
                    src => src.FullName.Split(' ', StringSplitOptions.None)
                        .LastOrDefault());

            return config;
        }
    }
}