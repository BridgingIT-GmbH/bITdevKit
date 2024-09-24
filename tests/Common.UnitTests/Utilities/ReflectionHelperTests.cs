// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

[UnitTest("Common")]
public class ReflectionHelperTests
{
    [Fact]
    public void CanSetProperties()
    {
        // Arrange
        var items = new Dictionary<string, object> { ["FirstName"] = "John", ["LastName"] = "Doe", ["age"] = 99, ["Title"] = "Sir" };
        var stub = new StubPerson();

        // Act
        ReflectionHelper.SetProperties(stub, items);

        // Assert
        stub.FirstName.ShouldBe("John");
        stub.LastName.ShouldBe("Doe");
        stub.Age.ShouldBe(99);
        stub.Title.ShouldBe("Sir");
    }

    [Fact]
    public void CanSetMultipleProperties()
    {
        // Arrange
        var stub = new StubPerson();

        // Act
        ReflectionHelper.SetProperty(stub, "FirstName", "John");
        ReflectionHelper.SetProperty(stub, "LastName", "Doe");
        ReflectionHelper.SetProperty(stub, "age", 99);
        ReflectionHelper.SetProperty(stub, "YearBorn", 1980);
        ReflectionHelper.SetProperty(stub, "title", "Sir");
        ReflectionHelper.SetProperty(stub, "CompanyName", "Acme");

        // Assert
        stub.FirstName.ShouldBe("John");
        stub.LastName.ShouldBe("Doe");
        stub.Age.ShouldBe(99);
        stub.YearBorn.ShouldBe(1980);
        stub.Title.ShouldBe("Sir");
        stub.CompanyName.ShouldBe("Acme");
    }

    [Fact]
    public void CanSetPrivateProperty()
    {
        // Arrange
        var stub = new StubPerson();

        // Act
        ReflectionHelper.SetProperty(stub, "CompanyName", "Acme");

        // Assert
        stub.CompanyName.ShouldBe("Acme");
    }

    [Fact]
    public void CanGetMultipleProperties()
    {
        // Arrange
        var stub = new StubPerson
        {
            FirstName = "John",
            LastName = "Doe",
            Age = 99,
            YearBorn = 1980,
            Title = "Sir"
        };

        // Act
        // Assert
        ReflectionHelper.GetProperty<string>(stub, "FirstName")
            .ShouldBe("John");
        ReflectionHelper.GetProperty<int>(stub, "FirstName")
            .ShouldBe(0);
        ReflectionHelper.GetProperty<string>(stub, "LastName")
            .ShouldBe("Doe");
        ReflectionHelper.GetProperty<int>(stub, "age")
            .ShouldBe(99);
        ReflectionHelper.GetProperty<string>(stub, "age")
            .ShouldBeNull();
        ReflectionHelper.GetProperty<int>(stub, "YearBorn")
            .ShouldBe(1980);
        ReflectionHelper.GetProperty<string>(stub, "title")
            .ShouldBe("Sir");
    }

    [Fact]
    public void CanSetAndConvertProperties()
    {
        // Arrange
        var items = new Dictionary<string, object> { ["FirstName"] = "John", ["LastName"] = "Doe", ["age"] = "99" };
        var stub = new StubPerson();

        // Act
        ReflectionHelper.SetProperties(stub, items);

        // Assert
        stub.FirstName.ShouldBe("John");
        stub.LastName.ShouldBe("Doe");
        stub.Age.ShouldBe(99);
        stub.Title.ShouldBe("Unknown");
    }

    [Fact]
    public void CanSetNullableProperties()
    {
        // Arrange
        var items = new Dictionary<string, object> { ["FirstName"] = "John", ["LastName"] = "Doe", ["age"] = 99, ["YearBorn"] = "1980" };
        var stub = new StubPerson();

        // Act
        ReflectionHelper.SetProperties(stub, items);

        // Assert
        stub.FirstName.ShouldBe("John");
        stub.LastName.ShouldBe("Doe");
        stub.Age.ShouldBe(99);
        stub.YearBorn.ShouldBe(1980);
    }

    [Fact]
    public void CanSetPropertiesToNull()
    {
        // Arrange
        var items = new Dictionary<string, object> { ["FirstName"] = null, ["YearBorn"] = null };
        var stub = new StubPerson { FirstName = "John", YearBorn = 1980 };

        // Act
        ReflectionHelper.SetProperties(stub, items);

        // Assert
        stub.FirstName.ShouldBe(null);
        stub.YearBorn.ShouldBe(null);
    }

    [Fact]
    public void CanSetInitOnlyProperties()
    {
        // Arrange
        var items = new Dictionary<string, object> { ["Country"] = "USA" };
        var stub = new StubPerson();

        // Act
        ReflectionHelper.SetProperties(stub, items);

        // Assert
        stub.Country.ShouldBe("USA");
    }

    [Fact]
    public void FindTypesTest()
    {
        // Arrange & Act
        var result = ReflectionHelper.FindTypes(t => typeof(StubPerson).IsAssignableFrom(t));

        // Assert
        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
        result.FirstOrDefault()
            .Name.ShouldBe(nameof(StubPerson));
    }

    public class StubPerson
    {
        public StubPerson() { }

        public StubPerson(string firstName)
        {
            this.FirstName = firstName;
        }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public int Age { get; set; }

        public int? YearBorn { get; set; }

        public string Title { get; set; } = "Unknown";

        public string Country { get; init; }

        // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
        public string CompanyName { get; private set; } = "Unknown";
    }
}