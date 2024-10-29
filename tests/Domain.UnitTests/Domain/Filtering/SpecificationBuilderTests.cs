// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain;

using System;
using System.Linq;
using Xunit;

public class SpecificationBuilderTests
{
    public SpecificationBuilderTests()
    {
        SpecificationResolver.Clear();
        SpecificationResolver.Register<PersonStub, AdultSpecification>("IsAdult");
        SpecificationResolver.Register<PersonStub, NameStartsWithSpecification>("NameStartsWith");
    }

    [Fact]
    public void BuildSpecifications_WithValidFilters_ReturnsCorrectSpecifications()
    {
        // Arrange
        var filters = new[]
        {
            new FilterCriteria { Field = "FirstName", Operator = FilterOperator.Equal, Value = "John" },
            new FilterCriteria { Field = "Age", Operator = FilterOperator.GreaterThanOrEqual, Value = 18 }
        };

        // Act
        var result = SpecificationBuilder.Build<PersonStub>(filters);

        // Assert
        result.Count().ShouldBe(2);
        result.ShouldAllBe(spec => spec.GetType().ImplementsInterface<ISpecification<PersonStub>>());
    }

    [Fact]
    public void BuildSpecifications_WithChildFilter_ReturnsCorrectSpecification()
    {
        // Arrange
        var filters = new[]
        {
            new FilterCriteria
            {
                Field = "BillingAddress.City",
                Operator = FilterOperator.Equal,
                Value = "Berlin"
            }
        };

        // Act
        var result = SpecificationBuilder.Build<PersonStub>(filters);

        // Assert
        result.Count().ShouldBe(1);
        var spec = result.First();

        var matchingPerson = new PersonStub
        {
            BillingAddress = AddressStub.Create("Billing", "123 Bill St", "", "10115", "Berlin", "Germany")
        };
        var nonMatchingPerson = new PersonStub
        {
            BillingAddress = AddressStub.Create("Billing", "456 Invoice Rd", "", "80331", "Munich", "Germany")
        };

        spec.IsSatisfiedBy(matchingPerson).ShouldBeTrue();
        spec.IsSatisfiedBy(nonMatchingPerson).ShouldBeFalse();
    }

    [Fact]
    public void BuildSpecifications_WithEqualOperator_ReturnsCorrectSpecification()
    {
        // Arrange
        var filters = new[] { new FilterCriteria { Field = "FirstName", Operator = FilterOperator.Equal, Value = "John" } };

        // Act
        var result = SpecificationBuilder.Build<PersonStub>(filters);

        // Assert
        result.Count().ShouldBe(1);
        var spec = result.First();
        spec.IsSatisfiedBy(new PersonStub { FirstName = "John" }).ShouldBeTrue();
        spec.IsSatisfiedBy(new PersonStub { FirstName = "Jane" }).ShouldBeFalse();
    }

    [Fact]
    public void BuildSpecifications_WithNotEqualOperator_ReturnsCorrectSpecification()
    {
        // Arrange
        var filters = new[] { new FilterCriteria { Field = "FirstName", Operator = FilterOperator.NotEqual, Value = "John" } };

        // Act
        var result = SpecificationBuilder.Build<PersonStub>(filters);

        // Assert
        result.Count().ShouldBe(1);
        var spec = result.First();
        spec.IsSatisfiedBy(new PersonStub { FirstName = "John" }).ShouldBeFalse();
        spec.IsSatisfiedBy(new PersonStub { FirstName = "Jane" }).ShouldBeTrue();
    }

    [Fact]
    public void BuildSpecifications_WithGreaterThanOperator_ReturnsCorrectSpecification()
    {
        // Arrange
        var filters = new[] { new FilterCriteria { Field = "Age", Operator = FilterOperator.GreaterThan, Value = 18 } };

        // Act
        var result = SpecificationBuilder.Build<PersonStub>(filters);

        // Assert
        result.Count().ShouldBe(1);
        var spec = result.First();
        spec.IsSatisfiedBy(new PersonStub { Age = 20 }).ShouldBeTrue();
        spec.IsSatisfiedBy(new PersonStub { Age = 18 }).ShouldBeFalse();
        spec.IsSatisfiedBy(new PersonStub { Age = 16 }).ShouldBeFalse();
    }

    [Fact]
    public void BuildSpecifications_WithGreaterThanOrEqualOperator_ReturnsCorrectSpecification()
    {
        // Arrange
        var filters = new[] { new FilterCriteria { Field = "Age", Operator = FilterOperator.GreaterThanOrEqual, Value = 18 } };

        // Act
        var result = SpecificationBuilder.Build<PersonStub>(filters);

        // Assert
        result.Count().ShouldBe(1);
        var spec = result.First();
        spec.IsSatisfiedBy(new PersonStub { Age = 20 }).ShouldBeTrue();
        spec.IsSatisfiedBy(new PersonStub { Age = 18 }).ShouldBeTrue();
        spec.IsSatisfiedBy(new PersonStub { Age = 16 }).ShouldBeFalse();
    }

    [Fact]
    public void BuildSpecifications_WithLessThanOperator_ReturnsCorrectSpecification()
    {
        // Arrange
        var filters = new[] { new FilterCriteria { Field = "Age", Operator = FilterOperator.LessThan, Value = 18 } };

        // Act
        var result = SpecificationBuilder.Build<PersonStub>(filters);

        // Assert
        result.Count().ShouldBe(1);
        var spec = result.First();
        spec.IsSatisfiedBy(new PersonStub { Age = 16 }).ShouldBeTrue();
        spec.IsSatisfiedBy(new PersonStub { Age = 18 }).ShouldBeFalse();
        spec.IsSatisfiedBy(new PersonStub { Age = 20 }).ShouldBeFalse();
    }

    [Fact]
    public void BuildSpecifications_WithLessThanOrEqualOperator_ReturnsCorrectSpecification()
    {
        // Arrange
        var filters = new[] { new FilterCriteria { Field = "Age", Operator = FilterOperator.LessThanOrEqual, Value = 18 } };

        // Act
        var result = SpecificationBuilder.Build<PersonStub>(filters);

        // Assert
        result.Count().ShouldBe(1);
        var spec = result.First();
        spec.IsSatisfiedBy(new PersonStub { Age = 16 }).ShouldBeTrue();
        spec.IsSatisfiedBy(new PersonStub { Age = 18 }).ShouldBeTrue();
        spec.IsSatisfiedBy(new PersonStub { Age = 20 }).ShouldBeFalse();
    }

    [Fact]
    public void BuildSpecifications_WithContainsOperator_ReturnsCorrectSpecification()
    {
        // Arrange
        var filters = new[] { new FilterCriteria { Field = "FirstName", Operator = FilterOperator.Contains, Value = "oh" } };

        // Act
        var result = SpecificationBuilder.Build<PersonStub>(filters);

        // Assert
        result.Count().ShouldBe(1);
        var spec = result.First();
        spec.IsSatisfiedBy(new PersonStub { FirstName = "John" }).ShouldBeTrue();
        spec.IsSatisfiedBy(new PersonStub { FirstName = "Jane" }).ShouldBeFalse();
    }

    [Fact]
    public void BuildSpecifications_WithStartsWithOperator_ReturnsCorrectSpecification()
    {
        // Arrange
        var filters = new[] { new FilterCriteria { Field = "FirstName", Operator = FilterOperator.StartsWith, Value = "Jo" } };

        // Act
        var result = SpecificationBuilder.Build<PersonStub>(filters);

        // Assert
        result.Count().ShouldBe(1);
        var spec = result.First();
        spec.IsSatisfiedBy(new PersonStub { FirstName = "John" }).ShouldBeTrue();
        spec.IsSatisfiedBy(new PersonStub { FirstName = "Jane" }).ShouldBeFalse();
    }

    [Fact]
    public void BuildSpecifications_WithEndsWithOperator_ReturnsCorrectSpecification()
    {
        // Arrange
        var filters = new[] { new FilterCriteria { Field = "FirstName", Operator = FilterOperator.EndsWith, Value = "hn" } };

        // Act
        var result = SpecificationBuilder.Build<PersonStub>(filters);

        // Assert
        result.Count().ShouldBe(1);
        var spec = result.First();
        spec.IsSatisfiedBy(new PersonStub { FirstName = "John" }).ShouldBeTrue();
        spec.IsSatisfiedBy(new PersonStub { FirstName = "Jane" }).ShouldBeFalse();
    }

    // [Fact]
    // public void BuildSpecifications_WithUnsupportedOperator_ThrowsNotSupportedException()
    // {
    //     // Arrange
    //     var filters = new[] { new FilterCriteria { Name = "FirstName", Operator = "unsupported", Value = "John" } };
    //
    //     // Act & Assert
    //     Should.Throw<NotSupportedException>(() => SpecificationBuilder.Build<PersonStub>(filters));
    // }

    [Fact]
    public void BuildSpecifications_WithAnyOperatorAndValue_ReturnsCorrectSpecification()
    {
        // Arrange
        var filters = new[]
        {
            new FilterCriteria
            {
                Field = "Addresses",
                Operator = FilterOperator.Any,
                Value = new FilterCriteria
                {
                    Field = "City", Operator = FilterOperator.Equal, Value = "Berlin"
                }
            }
        };

        // Act
        var result = SpecificationBuilder.Build<PersonStub>(filters);

        // Assert
        result.Count().ShouldBe(1);
        var spec = result.First();

        var matchingPerson = new PersonStub
        {
            Addresses =
            [
                AddressStub.Create("Home", "123 Main St", "", "12345", "New York", "USA"),
                AddressStub.Create("Work", "456 Office Blvd", "", "10115", "Berlin", "Germany")
            ]
        };
        var nonMatchingPerson = new PersonStub
        {
            Addresses =
            [
                AddressStub.Create("Home", "123 Main St", "", "12345", "New York", "USA"),
                AddressStub.Create("Work", "456 Office Blvd", "", "80331", "Munich", "Germany")
            ]
        };

        spec.IsSatisfiedBy(matchingPerson).ShouldBeTrue();
        spec.IsSatisfiedBy(nonMatchingPerson).ShouldBeFalse();
    }

    [Fact]
    public void BuildSpecifications_WithAnyOperatorAndFilters_ReturnsCorrectSpecification()
    {
        // Arrange
        var filters = new[]
        {
            new FilterCriteria
            {
                Field = "Addresses",
                Operator = FilterOperator.Any,
                Filters = new[]
                {
                    new FilterCriteria
                    {
                        Field = "City", Operator = FilterOperator.Equal, Value = "Berlin"
                    }
                }.ToList()
            }
        };

        // Act
        var result = SpecificationBuilder.Build<PersonStub>(filters);

        // Assert
        result.Count().ShouldBe(1);
        var spec = result.First();

        var matchingPerson = new PersonStub
        {
            Addresses =
            [
                AddressStub.Create("Home", "123 Main St", "", "12345", "New York", "USA"),
                AddressStub.Create("Work", "456 Office Blvd", "", "10115", "Berlin", "Germany")
            ]
        };
        var nonMatchingPerson = new PersonStub
        {
            Addresses =
            [
                AddressStub.Create("Home", "123 Main St", "", "12345", "New York", "USA"),
                AddressStub.Create("Work", "456 Office Blvd", "", "80331", "Munich", "Germany")
            ]
        };

        spec.IsSatisfiedBy(matchingPerson).ShouldBeTrue();
        spec.IsSatisfiedBy(nonMatchingPerson).ShouldBeFalse();
    }

    [Fact]
    public void BuildSpecifications_WithAllOperatorAndValue_ReturnsCorrectSpecification()
    {
        // Arrange
        var filters = new[]
        {
            new FilterCriteria
            {
                Field = "Addresses",
                Operator = FilterOperator.All,
                Value = new FilterCriteria
                {
                    Field = "Country", Operator = FilterOperator.Equal, Value = "Germany"
                }
            }
        };

        // Act
        var result = SpecificationBuilder.Build<PersonStub>(filters);

        // Assert
        result.Count().ShouldBe(1);
        var spec = result.First();

        var matchingPerson = new PersonStub
        {
            Addresses =
            [
                AddressStub.Create("Home", "123 Main St", "", "10115", "Berlin", "Germany"),
                AddressStub.Create("Work", "456 Office Blvd", "", "80331", "Munich", "Germany")
            ]
        };
        var nonMatchingPerson = new PersonStub
        {
            Addresses =
            [
                AddressStub.Create("Home", "123 Main St", "", "12345", "New York", "USA"),
                AddressStub.Create("Work", "456 Office Blvd", "", "10115", "Berlin", "Germany")
            ]
        };

        spec.IsSatisfiedBy(matchingPerson).ShouldBeTrue();
        spec.IsSatisfiedBy(nonMatchingPerson).ShouldBeFalse();
    }

    [Fact]
    public void BuildSpecifications_WithNoneOperatorAndValue_ReturnsCorrectSpecification()
    {
        // Arrange
        var filters = new[]
        {
            new FilterCriteria
            {
                Field = "Addresses",
                Operator = FilterOperator.None,
                Value = new FilterCriteria
                {
                    Field = "PostalCode", Operator = FilterOperator.StartsWith, Value = "1"
                }
            }
        };

        // Act
        var result = SpecificationBuilder.Build<PersonStub>(filters);

        // Assert
        result.Count().ShouldBe(1);
        var spec = result.First();

        var matchingPerson = new PersonStub
        {
            Addresses =
            [
                AddressStub.Create("Home", "123 Main St", "", "20115", "Hamburg", "Germany"),
                AddressStub.Create("Work", "456 Office Blvd", "", "80331", "Munich", "Germany")
            ]
        };
        var nonMatchingPerson = new PersonStub
        {
            Addresses =
            [
                AddressStub.Create("Home", "123 Main St", "", "12345", "New York", "USA"),
                AddressStub.Create("Work", "456 Office Blvd", "", "10115", "Berlin", "Germany")
            ]
        };

        spec.IsSatisfiedBy(matchingPerson).ShouldBeTrue();
        spec.IsSatisfiedBy(nonMatchingPerson).ShouldBeFalse();
    }

    [Fact]
    public void BuildSpecifications_WithLogicalOperatorOR_ReturnsCorrectSpecification()
    {
        // Arrange
        var filters = new[]
        {
            new FilterCriteria { Field = "FirstName", Operator = FilterOperator.Equal, Value = "John", Logic = FilterLogicOperator.Or },
            new FilterCriteria { Field = "LastName", Operator = FilterOperator.Equal, Value = "Doe" }
        };

        // Act
        var result = SpecificationBuilder.Build<PersonStub>(filters);

        // Assert
        Assert.Single(result);
        var spec = result.First();
        Assert.IsAssignableFrom<ISpecification<PersonStub>>(spec);

        // Verify the OR logic
        var testPerson1 = new PersonStub { FirstName = "John", LastName = "Smith" };
        var testPerson2 = new PersonStub { FirstName = "Jane", LastName = "Doe" };
        Assert.True(spec.IsSatisfiedBy(testPerson1));
        Assert.True(spec.IsSatisfiedBy(testPerson2));
    }

    [Fact]
    public void BuildSpecifications_WithMixedLogicalOperators_ReturnsCorrectSpecifications()
    {
        // Arrange
        var filters = new[]
        {
            new FilterCriteria { Field = "FirstName", Operator = FilterOperator.Equal, Value = "John", Logic = FilterLogicOperator.Or },
            new FilterCriteria { Field = "LastName", Operator = FilterOperator.Equal, Value = "Doe", Logic = FilterLogicOperator.And },
            new FilterCriteria { Field = "Age", Operator = FilterOperator.GreaterThanOrEqual, Value = 18 }
        };

        // Act
        var result = SpecificationBuilder.Build<PersonStub>(filters);

        // Assert
        Assert.Equal(2, result.Count());

        var orSpec = result.First();
        var andSpec = result.Last();

        var testPerson1 = new PersonStub { FirstName = "John", LastName = "Smith", Age = 25 };
        var testPerson2 = new PersonStub { FirstName = "Jane", LastName = "Doe", Age = 30 };
        var testPerson3 = new PersonStub { FirstName = "Alice", LastName = "Doe", Age = 17 };

        Assert.True(orSpec.IsSatisfiedBy(testPerson1));
        Assert.True(orSpec.IsSatisfiedBy(testPerson2));
        Assert.True(andSpec.IsSatisfiedBy(testPerson2));
        Assert.False(andSpec.IsSatisfiedBy(testPerson3));
    }

    [Fact]
    public void BuildSpecifications_WithFullTextSearch_ReturnsCorrectSpecification()
    {
        // Arrange
        var filters = new[]
        {
            new FilterCriteria
            {
                CustomType = FilterCustomType.FullTextSearch,
                CustomParameters = new Dictionary<string, object>
                {
                    ["searchTerm"] = "John",
                    ["fields"] = new[] { "FirstName", "LastName" }
                }
            }
        };

        // Act
        var result = SpecificationBuilder.Build<PersonStub>(filters);

        // Assert
        Assert.Single(result);
        var spec = result.First();

        var matchingPerson1 = new PersonStub { FirstName = "John", LastName = "Doe" };
        var matchingPerson2 = new PersonStub { FirstName = "Jane", LastName = "Johnson" };
        var nonMatchingPerson = new PersonStub { FirstName = "Alice", LastName = "Smith" };

        Assert.True(spec.IsSatisfiedBy(matchingPerson1));
        Assert.True(spec.IsSatisfiedBy(matchingPerson2));
        Assert.False(spec.IsSatisfiedBy(nonMatchingPerson));
    }

    [Fact]
    public void BuildSpecifications_WithDateRange_ReturnsCorrectSpecification()
    {
        // Arrange
        var filters = new[]
        {
            new FilterCriteria
            {
                CustomType = FilterCustomType.DateRange,
                CustomParameters = new Dictionary<string, object>
                {
                    ["field"] = "BirthDate",
                    ["startDate"] = new DateTime(1990, 1, 1).ToString("o"),
                    ["endDate"] = new DateTime(2000, 12, 31).ToString("o")
                }
            }
        };

        // Act
        var result = SpecificationBuilder.Build<PersonStub>(filters);

        // Assert
        Assert.Single(result);
        var spec = result.First();

        var matchingPerson = new PersonStub { BirthDate = new DateTime(1995, 6, 15) };
        var nonMatchingPerson1 = new PersonStub { BirthDate = new DateTime(1989, 12, 31) };
        var nonMatchingPerson2 = new PersonStub { BirthDate = new DateTime(2001, 1, 1) };

        Assert.True(spec.IsSatisfiedBy(matchingPerson));
        Assert.False(spec.IsSatisfiedBy(nonMatchingPerson1));
        Assert.False(spec.IsSatisfiedBy(nonMatchingPerson2));
    }

    [Fact]
    public void BuildSpecifications_WithNumericRange_ReturnsCorrectSpecification()
    {
        // Arrange
        var filters = new[]
        {
            new FilterCriteria
            {
                CustomType = FilterCustomType.NumericRange,
                CustomParameters = new Dictionary<string, object>
                {
                    ["field"] = "Age",
                    ["min"] = 18,
                    ["max"] = 65,
                    ["inclusive"] = true // also the default
                }
            }
        };

        // Act
        var result = SpecificationBuilder.Build<PersonStub>(filters);

        // Assert
        Assert.Single(result);
        var spec = result.First();

        var matchingPerson1 = new PersonStub { Age = 30 };
        var matchingPerson2 = new PersonStub { Age = 65 };
        var nonMatchingPersonYoung = new PersonStub { Age = 17 };
        var nonMatchingPersonOld = new PersonStub { Age = 66 };

        Assert.True(spec.IsSatisfiedBy(matchingPerson1));
        Assert.True(spec.IsSatisfiedBy(matchingPerson2));
        Assert.False(spec.IsSatisfiedBy(nonMatchingPersonYoung));
        Assert.False(spec.IsSatisfiedBy(nonMatchingPersonOld));
    }

    [Fact]
    public void BuildSpecifications_WithNotNull_ReturnsCorrectSpecification()
    {
        // Arrange
        var filters = new[]
        {
            new FilterCriteria
            {
                CustomType = FilterCustomType.IsNotNull,
                CustomParameters = new Dictionary<string, object>
                {
                    ["field"] = "Email"
                }
            }
        };

        // Act
        var result = SpecificationBuilder.Build<PersonStub>(filters);

        // Assert
        Assert.Single(result);
        var spec = result.First();

        var matchingPerson = new PersonStub { Email = "test@example.com" };
        var nonMatchingPerson = new PersonStub { Email = null };

        Assert.True(spec.IsSatisfiedBy(matchingPerson));
        Assert.False(spec.IsSatisfiedBy(nonMatchingPerson));
    }

    [Fact]
    public void BuildSpecifications_WithTimeRange_ReturnsCorrectSpecification()
    {
        // Arrange
        var filters = new[]
        {
            new FilterCriteria
            {
                CustomType = FilterCustomType.TimeRange,
                CustomParameters = new Dictionary<string, object>
                {
                    ["field"] = "WorkStartTime",
                    ["startTime"] = "09:00:00", // 9:00 AM
                    ["endTime"] = "17:00:00" // 5:00 PM
                }
            }
        };

        // Act
        var result = SpecificationBuilder.Build<PersonStub>(filters);

        // Assert
        result.Count().ShouldBe(1);
        var spec = result.First();

        var matchingPerson = new PersonStub { WorkStartTime = TimeSpan.FromHours(12) }; // 12:00 PM
        var nonMatchingPersonEarly = new PersonStub { WorkStartTime = TimeSpan.FromHours(8) }; // 8:00 AM
        var nonMatchingPersonLate = new PersonStub { WorkStartTime = TimeSpan.FromHours(18) }; // 6:00 PM

        spec.IsSatisfiedBy(matchingPerson).ShouldBeTrue();
        spec.IsSatisfiedBy(nonMatchingPersonEarly).ShouldBeFalse();
        spec.IsSatisfiedBy(nonMatchingPersonLate).ShouldBeFalse();
    }

    [Fact]
    public void BuildSpecifications_WithOvernightTimeRange_ReturnsCorrectSpecification()
    {
        // Arrange
        var filters = new[]
        {
            new FilterCriteria
            {
                CustomType = FilterCustomType.TimeRange,
                CustomParameters = new Dictionary<string, object>
                {
                    ["field"] = "WorkStartTime",
                    ["startTime"] = "22:00:00", // 10:00 PM
                    ["endTime"] = "06:00:00" // 6:00 AM
                }
            }
        };

        // Act
        var result = SpecificationBuilder.Build<PersonStub>(filters);

        // Assert
        result.Count().ShouldBe(1);
        var spec = result.First();

        var matchingPersonLate = new PersonStub { WorkStartTime = TimeSpan.FromHours(23) }; // 11:00 PM
        var matchingPersonEarly = new PersonStub { WorkStartTime = TimeSpan.FromHours(2) }; // 2:00 AM
        var nonMatchingPerson = new PersonStub { WorkStartTime = TimeSpan.FromHours(12) }; // 12:00 PM

        spec.IsSatisfiedBy(matchingPersonLate).ShouldBeTrue();
        spec.IsSatisfiedBy(matchingPersonEarly).ShouldBeTrue();
        spec.IsSatisfiedBy(nonMatchingPerson).ShouldBeFalse();
    }

    [Fact]
    public void BuildSpecifications_WithInvalidTimeRangeFormat_ThrowsArgumentException()
    {
        // Arrange
        var filters = new[]
        {
            new FilterCriteria
            {
                CustomType = FilterCustomType.TimeRange,
                CustomParameters = new Dictionary<string, object>
                {
                    ["field"] = "WorkStartTime",
                    ["startTime"] = "invalid time",
                    ["endTime"] = "17:00:00"
                }
            }
        };

        // Act & Assert
        Should.Throw<ArgumentException>(() => SpecificationBuilder.Build<PersonStub>(filters));
    }

    [Fact]
    public void BuildSpecifications_WithEnumFilter_ReturnsCorrectSpecification()
    {
        // Arrange
        var filters = new[]
        {
            new FilterCriteria
            {
                CustomType = FilterCustomType.EnumValues,
                CustomParameters = new Dictionary<string, object>
                {
                    ["field"] = "EmploymentStatus",
                    ["values"] = "FullTime;PartTime"
                }
            }
        };

        // Act
        var result = SpecificationBuilder.Build<PersonStub>(filters);

        // Assert
        Assert.Single(result);
        var spec = result.First();

        var matchingPersonFullTime = new PersonStub { EmploymentStatus = EmploymentStatus.FullTime };
        var matchingPersonPartTime = new PersonStub { EmploymentStatus = EmploymentStatus.PartTime };
        var nonMatchingPerson = new PersonStub { EmploymentStatus = EmploymentStatus.Contractor };

        Assert.True(spec.IsSatisfiedBy(matchingPersonFullTime));
        Assert.True(spec.IsSatisfiedBy(matchingPersonPartTime));
        Assert.False(spec.IsSatisfiedBy(nonMatchingPerson));
    }

    [Fact]
    public void BuildSpecifications_WithEnumFilter_InvalidEnumValue_ThrowsArgumentException()
    {
        // Arrange
        var filters = new[]
        {
            new FilterCriteria
            {
                CustomType = FilterCustomType.EnumValues,
                CustomParameters = new Dictionary<string, object>
                {
                    ["field"] = "EmploymentStatus",
                    ["values"] = "FullTime;InvalidValue"
                }
            }
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => SpecificationBuilder.Build<PersonStub>(filters));
    }

    [Fact]
    public void BuildSpecifications_WithEnumFilter_UsingIntegerValues_ReturnsCorrectSpecification()
    {
        // Arrange
        var filters = new[]
        {
            new FilterCriteria
            {
                CustomType = FilterCustomType.EnumValues,
                CustomParameters = new Dictionary<string, object>
                {
                    ["field"] = "EmploymentStatus",
                    ["values"] = "0;1" // Assuming FullTime = 0, PartTime = 1
                }
            }
        };

        // Act
        var result = SpecificationBuilder.Build<PersonStub>(filters);

        // Assert
        Assert.Single(result);
        var spec = result.First();

        var matchingPersonFullTime = new PersonStub { EmploymentStatus = EmploymentStatus.FullTime };
        var matchingPersonPartTime = new PersonStub { EmploymentStatus = EmploymentStatus.PartTime };
        var nonMatchingPerson = new PersonStub { EmploymentStatus = EmploymentStatus.Contractor };

        Assert.True(spec.IsSatisfiedBy(matchingPersonFullTime));
        Assert.True(spec.IsSatisfiedBy(matchingPersonPartTime));
        Assert.False(spec.IsSatisfiedBy(nonMatchingPerson));
    }

    [Fact]
    public void BuildSpecifications_WithEnumFilter_MixedValues_ReturnsCorrectSpecification()
    {
        // Arrange
        var filters = new[]
        {
            new FilterCriteria
            {
                CustomType = FilterCustomType.EnumValues,
                CustomParameters = new Dictionary<string, object>
                {
                    ["field"] = "EmploymentStatus",
                    ["values"] = "FullTime;1" // Mixed use of name and integer value
                }
            }
        };

        // Act
        var result = SpecificationBuilder.Build<PersonStub>(filters);

        // Assert
        Assert.Single(result);
        var spec = result.First();

        var matchingPersonFullTime = new PersonStub { EmploymentStatus = EmploymentStatus.FullTime };
        var matchingPersonPartTime = new PersonStub { EmploymentStatus = EmploymentStatus.PartTime };
        var nonMatchingPerson = new PersonStub { EmploymentStatus = EmploymentStatus.Contractor };

        Assert.True(spec.IsSatisfiedBy(matchingPersonFullTime));
        Assert.True(spec.IsSatisfiedBy(matchingPersonPartTime));
        Assert.False(spec.IsSatisfiedBy(nonMatchingPerson));
    }

    [Fact]
    public void BuildSpecifications_WithNamedSpecifications_ReturnsCorrectSpecification()
    {
        // Arrange
        var filters = new[]
        {
            new FilterCriteria
            {
                CustomType = FilterCustomType.NamedSpecification,
                SpecificationName = "IsAdult",
                SpecificationArguments = [18]
            },
            new FilterCriteria
            {
                CustomType = FilterCustomType.NamedSpecification,
                SpecificationName = "NameStartsWith",
                SpecificationArguments = ["J"]
            }
        };

        // Act
        var result = SpecificationBuilder.Build<PersonStub>(filters);

        // Assert
        Assert.Equal(2, result.Count());
        Assert.All(result, spec => Assert.IsAssignableFrom<ISpecification<PersonStub>>(spec));

        // Verify the named specifications logic
        var testPerson1 = new PersonStub { FirstName = "John", Age = 20 };
        var testPerson2 = new PersonStub { FirstName = "Jane", Age = 17 };
        Assert.True(result.All(spec => spec.IsSatisfiedBy(testPerson1)));
        Assert.False(result.All(spec => spec.IsSatisfiedBy(testPerson2)));
    }

    [Fact]
    public void BuildSpecifications_WithCompositeSpecification_ReturnsCorrectSpecification()
    {
        // Arrange
        var filters = new[]
        {
            new FilterCriteria
            {
                CustomType = FilterCustomType.CompositeSpecification,
                CompositeSpecification = new CompositeSpecification
                {
                    Nodes =
                    [
                        new SpecificationLeaf { Name = "IsAdult", Arguments = [18] },
                        new SpecificationLeaf { Name = "NameStartsWith", Arguments = ["J"] }
                    ],
                    // LogicalOperator = LogicalOperator.And
                }
            }
        };

        // Act
        var result = SpecificationBuilder.Build<PersonStub>(filters);

        // Assert
        Assert.Single(result);
        var spec = result.First();
        Assert.IsAssignableFrom<ISpecification<PersonStub>>(spec);

        var testPerson1 = new PersonStub { FirstName = "John", Age = 20 };
        var testPerson2 = new PersonStub { FirstName = "Jane", Age = 25 };
        var testPerson3 = new PersonStub { FirstName = "Jack", Age = 17 };
        Assert.True(spec.IsSatisfiedBy(testPerson1));
        Assert.True(spec.IsSatisfiedBy(testPerson2));
        Assert.False(spec.IsSatisfiedBy(testPerson3));
    }

    [Fact]
    public void BuildSpecifications_WithEmptyInput_ReturnsEmptyList()
    {
        // Arrange
        var filters = Array.Empty<FilterCriteria>();

        // Act
        var result = SpecificationBuilder.Build<PersonStub>(filters);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void BuildSpecifications_WithNullInput_ReturnsEmptyList()
    {
        // Arrange
        FilterModel filterModel = null;

        // Act
        var result = SpecificationBuilder.Build<PersonStub>(filterModel);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void BuildSpecifications_WithChildStartsWithFilter_ReturnsCorrectSpecification()
    {
        // Arrange
        var filters = new[]
        {
            new FilterCriteria
            {
                Field = "BillingAddress.PostalCode",
                Operator = FilterOperator.StartsWith,
                Value = "10"
            }
        };

        // Act
        var result = SpecificationBuilder.Build<PersonStub>(filters);

        // Assert
        result.Count().ShouldBe(1);
        var spec = result.First();

        var matchingPerson = new PersonStub
        {
            BillingAddress = AddressStub.Create("Billing", "123 Bill St", "", "10115", "Berlin", "Germany")
        };
        var nonMatchingPerson = new PersonStub
        {
            BillingAddress = AddressStub.Create("Billing", "456 Invoice Rd", "", "80331", "Munich", "Germany")
        };

        spec.IsSatisfiedBy(matchingPerson).ShouldBeTrue();
        spec.IsSatisfiedBy(nonMatchingPerson).ShouldBeFalse();
    }

    [Fact]
    public void BuildSpecifications_WithChildEqualsFilter_ReturnsCorrectSpecification()
    {
        // Arrange
        var filters = new[]
        {
            new FilterCriteria
            {
                Field = "BillingAddress.Country",
                Operator = FilterOperator.Equal,
                Value = "Germany"
            }
        };

        // Act
        var result = SpecificationBuilder.Build<PersonStub>(filters);

        // Assert
        result.Count().ShouldBe(1);
        var spec = result.First();

        var matchingPerson = new PersonStub
        {
            BillingAddress = AddressStub.Create("Billing", "123 Bill St", "", "10115", "Berlin", "Germany")
        };
        var nonMatchingPerson = new PersonStub
        {
            BillingAddress = AddressStub.Create("Billing", "456 Invoice Rd", "", "NY 10001", "New York", "USA")
        };

        spec.IsSatisfiedBy(matchingPerson).ShouldBeTrue();
        spec.IsSatisfiedBy(nonMatchingPerson).ShouldBeFalse();
    }

    [Fact]
    public void BuildSpecifications_WithChildFilters_ReturnsCorrectSpecification()
    {
        // Arrange
        var filters = new[]
        {
            new FilterCriteria
            {
                Field = "BillingAddress.Country",
                Operator = FilterOperator.Equal,
                Value = "Germany"
            },
            new FilterCriteria
            {
                Field = "BillingAddress.PostalCode",
                Operator = FilterOperator.StartsWith,
                Value = "10"
            }
        };

        // Act
        var result = SpecificationBuilder.Build<PersonStub>(filters);

        // Assert
        result.Count().ShouldBe(2);
        var combinedSpec = result.Aggregate((spec1, spec2) => spec1.And(spec2));

        var matchingPerson = new PersonStub
        {
            BillingAddress = AddressStub.Create("Billing", "123 Bill St", "", "10115", "Berlin", "Germany")
        };
        var nonMatchingPerson1 = new PersonStub
        {
            BillingAddress = AddressStub.Create("Billing", "456 Invoice Rd", "", "80331", "Munich", "Germany")
        };
        var nonMatchingPerson2 = new PersonStub
        {
            BillingAddress = AddressStub.Create("Billing", "789 Receipt Ave", "", "10001", "New York", "USA")
        };

        combinedSpec.IsSatisfiedBy(matchingPerson).ShouldBeTrue();
        combinedSpec.IsSatisfiedBy(nonMatchingPerson1).ShouldBeFalse();
        combinedSpec.IsSatisfiedBy(nonMatchingPerson2).ShouldBeFalse();
    }

    [Fact]
    public void BuildSpecifications_WithNoChild_HandlesGracefully()
    {
        // Arrange
        var filters = new[]
        {
            new FilterCriteria
            {
                Field = "BillingAddress.City",
                Operator = FilterOperator.Equal,
                Value = "Berlin"
            }
        };

        // Act
        var result = SpecificationBuilder.Build<PersonStub>(filters);

        // Assert
        result.Count().ShouldBe(1);
        var spec = result.First();

        var person = new PersonStub
        {
            BillingAddress = null
        };

        spec.IsSatisfiedBy(person).ShouldBeFalse();
    }
}