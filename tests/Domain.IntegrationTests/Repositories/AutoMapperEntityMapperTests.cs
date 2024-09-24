// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.IntegrationTests.Repositories;

using Infrastructure.Mapping;

[IntegrationTest("Domain")]
public class AutoMapperEntityMapperTests
{
    [Fact]
    public void MapIdSpecification_Test()
    {
        // Arrange
        var dto1 = new StubDbEntity { Identifier = "111" };
        var dto2 = new StubDbEntity { Identifier = "333" };
        var specification = new StubHasIdSpecification("111");
        var sut = new AutoMapperEntityMapper(StubEntityMapperConfiguration.Create());

        // Act
        var result = sut.MapSpecification<StubEntity, StubDbEntity>(specification)
            .Compile();

        // Assert
        result(dto1)
            .ShouldBeTrue();
        result(dto2)
            .ShouldBeFalse();
    }

    [Fact]
    public void MapPropertySpecification_Test()
    {
        // Arrange
        var dto1 = new StubDbEntity { FullName = "John Doe" };
        var dto2 = new StubDbEntity { FullName = "John Does" };
        var specification = new StubHasNameSpecification("John", "Doe");
        var sut = new AutoMapperEntityMapper(StubEntityMapperConfiguration.Create());

        // Act
        var result = sut.MapSpecification<StubEntity, StubDbEntity>(specification)
            .Compile();

        // Assert
        result(dto1)
            .ShouldBeTrue();
        result(dto2)
            .ShouldBeFalse();
    }

    [Fact]
    public void MapInstance_Test()
    {
        // Arrange
        var dto1 = new StubDbEntity { Identifier = "111" };
        var sut = new AutoMapperEntityMapper(StubEntityMapperConfiguration.Create());

        // Act
        var result = sut.Map<StubEntity>(dto1);

        // Assert
        result.Id.ShouldBe("111");
    }

    [Fact]
    public void MapNullInstance_Test()
    {
        // Arrange
        StubDbEntity dto1 = null;
        var sut = new AutoMapperEntityMapper(StubEntityMapperConfiguration.Create());

        // Act
        var result = sut.Map<StubEntity>(dto1);

        // Assert
        result.ShouldBeNull();
    }
}