// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Mapping;

using global::AutoMapper;

[UnitTest("Common")]
public class AutoMapperProfileTests
{
    [Fact]
    public void CanMap_Test()
    {
        // Arrange
        var config = new MapperConfiguration(cfg => cfg.AddProfile<PersonProfile>());
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
        var config = new MapperConfiguration(cfg => cfg.AddProfile<PersonProfile>());
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
        var config = new MapperConfiguration(cfg => cfg.AddProfile<PersonProfile>());
        config.AssertConfigurationIsValid();
        var mapper = new AutoMapper<PersonStub, PersonDtoStub>(config.CreateMapper());

        // Act
        var target1 = mapper.Map(source);
        var target2 = mapper.Map(source, true);

        // Assert
        target1.ShouldBeNull();
        target2.ShouldNotBeNull();
        target2.Age.ShouldBe(0);
        target2.FullName.ShouldBe(string.Empty);
    }
}

public class PersonProfile : Profile
{
    public PersonProfile() => this.CreateMap<PersonStub, PersonDtoStub>()
            .ForMember(d => d.FullName, opt => opt.MapFrom(s => $"{s.FirstName} {s.LastName}".Trim()))
            .ReverseMap(); // https://dotnettutorials.net/lesson/reverse-mapping-using-automapper/
}