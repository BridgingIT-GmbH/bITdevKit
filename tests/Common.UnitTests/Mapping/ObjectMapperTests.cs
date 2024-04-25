// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Mappings;

[UnitTest("Common")]
public class ObjectMapperTests
{
    [Fact]
    public void CanMap_Test()
    {
        // Arrange
        var source = new PersonStub { FirstName = "John", LastName = "Doe" };
        var mapper = new ObjectMapper<PersonStub, PersonDtoStub>((s, d) => d.FullName = $"{s.FirstName} {s.LastName}");

        // Act
        var target = mapper.Map(source);

        // Assert
        target.ShouldNotBeNull();
        target.FullName.ShouldBe("John Doe");
    }

    [Fact]
    public void CanMap2_Test()
    {
        // Arrange
        var source = new PersonStub { Age = 25, FirstName = "John", LastName = "Doe" };
        var mapper = new ObjectMapper();
        mapper.For<PersonStub, PersonDtoStub>()
            .MapCustom(s => $"{s.FirstName} {s.LastName}", d => d.FullName)
            .Map(s => s.Age, d => d.Age)
            .Apply();

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
        var mapper = new ObjectMapper<PersonStub, PersonDtoStub>((s, d) => d.FullName = $"{s.FirstName} {s.LastName}");

        // Act
        var target1 = mapper.Map(source);
        var target2 = mapper.Map(source, true);

        // Assert
        target1.ShouldBeNull();
        target2.ShouldNotBeNull();
        target2.FullName.ShouldBe(" ");
    }
}