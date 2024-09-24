// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

[UnitTest("Common")]
public class FactoryTests
{
    [Fact]
    public void CanCreateInstance()
    {
        Factory<Stub>.Create()
            .ShouldNotBeNull();
        Factory<Stub>.Create("firstname")
            .ShouldNotBeNull();
        Factory<Stub>.Create("firstname", "NOARG")
            .ShouldBeNull();
        Factory.Create(typeof(Stub))
            .ShouldNotBeNull();
        Factory.Create<Stub>(typeof(Stub))
            .ShouldNotBeNull();
        Factory.Create<FactoryTests>(typeof(Stub))
            .ShouldBeNull();
        Factory.Create(typeof(Stub), "firstname")
            .ShouldNotBeNull();
        Factory.Create(typeof(Stub), "firstname", "NOARG")
            .ShouldBeNull();
    }

    [Fact]
    public void CanCreateGenericInstance()
    {
        Factory.Create(typeof(Stub<>), typeof(int))
            .ShouldNotBeNull();
        Factory.Create<Stub<int>>(typeof(Stub<>), typeof(int))
            .ShouldNotBeNull();
        Factory.Create<Stub<string>>(typeof(Stub<>), typeof(int))
            .ShouldBeNull();
        Factory.Create(typeof(Stub<>), typeof(int), "firstname")
            .ShouldNotBeNull();
        Factory.Create(typeof(Stub<>), typeof(int), "firstname", "NOARG")
            .ShouldBeNull();
    }

    [Fact]
    public void CanCreateWithDictionaryProperties1Instance()
    {
        // Arrange
        var properties = new Dictionary<string, object> { ["Firstname"] = "John", ["lastname"] = "Doe" };

        // Act
        var sut = Factory<Stub>.Create(properties);

        // Assert
        sut.FirstName.ShouldBe("John");
        sut.LastName.ShouldBe("Doe");
    }

    [Fact]
    public void CanCreateWithDictionaryProperties2Instance()
    {
        // Arrange
        var properties = new Dictionary<string, object> { ["Firstname"] = "John", ["lastname"] = "Doe" };

        // Act
        var sut = Factory.Create(typeof(Stub), properties) as Stub;

        // Assert
        sut.FirstName.ShouldBe("John");
        sut.LastName.ShouldBe("Doe");
    }

    public class Stub
    {
        public Stub() { }

        public Stub(string firstName)
        {
            this.FirstName = firstName;
        }

        public string FirstName { get; set; }

        public string LastName { get; set; }
    }

    public class Stub<T>
    {
        public Stub() { }

        public Stub(string firstName)
        {
            this.FirstName = firstName;
        }

        public string FirstName { get; set; }

        public string LastName { get; set; }
    }
}