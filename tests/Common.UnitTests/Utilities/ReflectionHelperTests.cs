// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using System.Collections.Generic;

[UnitTest("Common")]
public class ReflectionHelperTests
{
    [Fact]
    public void CanSetProperties()
    {
        // Arrange
        var items = new Dictionary<string, object>
        {
            ["FirstName"] = "John",
            ["LastName"] = "Doe",
            ["age"] = 99,
            ["Title"] = "Sir"
        };
        var stub = new Stub();

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
        var stub = new Stub();

        // Act
        ReflectionHelper.SetProperty(stub, "FirstName", "John");
        ReflectionHelper.SetProperty(stub, "LastName", "Doe");
        ReflectionHelper.SetProperty(stub, "age", 99);
        ReflectionHelper.SetProperty(stub, "YearBorn", 1980);
        ReflectionHelper.SetProperty(stub, "title", "Sir");

        // Assert
        stub.FirstName.ShouldBe("John");
        stub.LastName.ShouldBe("Doe");
        stub.Age.ShouldBe(99);
        stub.YearBorn.ShouldBe(1980);
        stub.Title.ShouldBe("Sir");
    }

    [Fact]
    public void CanGetMultipleProperties()
    {
        // Arrange
        var stub = new Stub()
        {
            FirstName = "John",
            LastName = "Doe",
            Age = 99,
            YearBorn = 1980,
            Title = "Sir"
        };

        // Act
        // Assert
        ReflectionHelper.GetProperty<string>(stub, "FirstName").ShouldBe("John");
        ReflectionHelper.GetProperty<int>(stub, "FirstName").ShouldBe(0);
        ReflectionHelper.GetProperty<string>(stub, "LastName").ShouldBe("Doe");
        ReflectionHelper.GetProperty<int>(stub, "age").ShouldBe(99);
        ReflectionHelper.GetProperty<string>(stub, "age").ShouldBeNull();
        ReflectionHelper.GetProperty<int>(stub, "YearBorn").ShouldBe(1980);
        ReflectionHelper.GetProperty<string>(stub, "title").ShouldBe("Sir");
    }

    [Fact]
    public void CanSetAndConvertProperties()
    {
        // Arrange
        var items = new Dictionary<string, object>
        {
            ["FirstName"] = "John",
            ["LastName"] = "Doe",
            ["age"] = "99"
        };
        var stub = new Stub();

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
        var items = new Dictionary<string, object>
        {
            ["FirstName"] = "John",
            ["LastName"] = "Doe",
            ["age"] = 99,
            ["YearBorn"] = "1980"
        };
        var stub = new Stub();

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
        var items = new Dictionary<string, object>
        {
            ["FirstName"] = null,
            ["YearBorn"] = null
        };
        var stub = new Stub
        {
            FirstName = "John",
            YearBorn = 1980
        };

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
        var items = new Dictionary<string, object>
        {
            ["Country"] = "USA"
        };
        var stub = new Stub();

        // Act
        ReflectionHelper.SetProperties(stub, items);

        // Assert
        stub.Country.ShouldBe("USA");
    }

    [Fact]
    public void FindTypesTest()
    {
        // Arrange & Act
        var result = ReflectionHelper.FindTypes(t => typeof(Stub).IsAssignableFrom(t));

        // Assert
        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
        result.FirstOrDefault().Name.ShouldBe(nameof(Stub));
    }

    public class Stub
    {
        public Stub()
        {
        }

        public Stub(string firstName)
        {
            this.FirstName = firstName;
        }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public int Age { get; set; }

        public int? YearBorn { get; set; }

        public string Title { get; set; } = "Unknown";

        public string Country { get; init; }
    }
}
